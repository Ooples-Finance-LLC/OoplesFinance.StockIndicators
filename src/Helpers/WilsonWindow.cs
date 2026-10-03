using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core.Registry;
using OoplesFinance.StockIndicators.Streaming;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class WilsonWindow : IDisposable
{
    private readonly Average _gains, _losses;
    private readonly Average[] _smooth;
    private readonly Number[] _thresholds;
    private Number _previous, _previousBull, _previousBear;
    private bool _hasPrevious;
    internal WilsonWindow(MovingAvgType kind, int length, int smooth, params double[] thresholds)
    {
        foreach (var threshold in thresholds) StreamingInputValidation.Finite(threshold, nameof(thresholds));
        _gains = new(kind, length); _losses = new(kind, length);
        _thresholds = thresholds.Select(Number.Of).ToArray(); _smooth = thresholds.Select(_ => new Average(kind, smooth)).ToArray();
    }
    private static Number Rsi(Number gain, Number loss)
    {
        if (loss.Sign == 0) return Number.Of(100); if (gain.Sign == 0 || (gain + loss).Sign == 0) return default;
        var value = gain.Times(100).Divide(gain + loss);
        return value.Sign < 0 ? default : (value - Number.Of(100)).Sign > 0 ? Number.Of(100) : value;
    }
    private static Number Band(Number price, Number distance) => price - (price * distance).Divide(100);
    private static (Number Bull, Number Bear) Margins(Number price, Number[] bands)
    {
        var upper = (bands[2] - bands[3]).Sign < 0 ? bands[2] : bands[3];
        var lower = (bands[0] - bands[1]).Sign > 0 ? bands[0] : bands[1];
        return (price - upper, price - lower);
    }
    private static Signal Trade(Number bull, Number bear, Number previousBull, Number previousBear)
        => bull.Sign > 0 && (bull - previousBull).Sign > 0 ? Signal.StrongBuy
            : bear.Sign < 0 && (bear - previousBear).Sign < 0 ? Signal.StrongSell
            : bull.Sign > 0 ? Signal.Buy : bear.Sign < 0 ? Signal.Sell : Signal.None;
    internal (double[] Lines, Signal Trade) Next(double price, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price)); var current = Number.Of(price); var delta = _hasPrevious ? current - _previous : default;
        var gain = _gains.Next(delta.Sign > 0 ? delta : default, final); var loss = _losses.Next(delta.Sign < 0 ? delta.Times(-1) : default, final);
        var rsi = Rsi(gain, loss); var bands = _thresholds.Select((threshold, i) => Band(current, _smooth[i].Next(rsi - threshold, final))).ToArray();
        var margins = bands.Length == 4 ? Margins(current, bands) : default;
        var trade = bands.Length == 4 ? Trade(margins.Bull, margins.Bear, _previousBull, _previousBear) : Signal.None;
        if (final) { _previous = current; _hasPrevious = true; _previousBull = margins.Bull; _previousBear = margins.Bear; }
        return (bands.Select(v => v.Publish()).ToArray(), trade);
    }
    internal static (double[][] Lines, Signal[] Trades) Calculate(StockData data, MovingAvgType kind, int length, int smooth,
        double[] thresholds, bool fast = false)
    {
        length = Math.Max(1, length); smooth = Math.Max(1, smooth);
        foreach (var threshold in thresholds) StreamingInputValidation.Finite(threshold, nameof(thresholds));
        var (input, _, _, _, volumes) = CalculationsHelper.GetInputValuesList(data);
        foreach (var series in new[] { input, data.OpenPrices, data.HighPrices, data.LowPrices, data.ClosePrices, volumes })
            foreach (var value in series) StreamingInputValidation.Finite(value, nameof(data));
        var prices = input.Select(Number.Of).ToArray(); var gains = new Number[prices.Length]; var losses = new Number[prices.Length];
        for (var i = 1; i < prices.Length; i++) { var delta = prices[i] - prices[i - 1]; if (delta.Sign > 0) gains[i] = delta; else losses[i] = delta.Times(-1); }
        Number[] Mean(Number[] values, int period, bool hooks)
        {
            var replacement = hooks ? ComponentAverage.Take(values.Select(v => v.Publish()).ToArray(), period) : null;
            if (replacement is not null) return Enumerable.Range(0, prices.Length).Select(i => i < replacement.Count ? Number.Of(replacement[i]) : default).ToArray();
            if (StrengthWindow.Supports(kind)) { using var mean = new Average(kind, period); return values.Select(v => mean.Next(v, true)).ToArray(); }
            var raw = values.Select(v => v.Publish()).ToArray();
            if (!fast) return CalculationsHelper.GetMovingAverageList(data, kind, period, raw.ToList()).Select(Number.Of).ToArray();
            var result = new double[raw.Length]; MovingAverageRegistry.GetRequired(kind).Compute(raw, result, period); return result.Select(Number.Of).ToArray();
        }
        // The original fast RSI consumed gain/loss override slots only while overrides were armed.
        var rsiHooks = fast && ComponentAverage.HasOverrides;
        var gainMeans = Mean(gains, length, rsiHooks); var lossMeans = Mean(losses, length, rsiHooks);
        var rsi = prices.Select((_, i) => Rsi(gainMeans[i], lossMeans[i])).ToArray();
        if (fast && kind == MovingAvgType.ExponentialMovingAverage && length > 1)
            for (var i = 1; i < rsi.Length; i++) if ((prices[i] - prices[i - 1]).Sign == 0) rsi[i] = rsi[i - 1];
        var bands = thresholds.Select(threshold => Mean(rsi.Select(v => v - Number.Of(threshold)).ToArray(), smooth, fast)
            .Select((distance, i) => Band(prices[i], distance)).ToArray()).ToArray();
        var trades = new Signal[prices.Length]; Number previousBull = default, previousBear = default;
        if (thresholds.Length == 4) for (var i = 0; i < trades.Length; i++)
        {
            var margins = Margins(prices[i], bands.Select(line => line[i]).ToArray());
            trades[i] = Trade(margins.Bull, margins.Bear, previousBull, previousBear); previousBull = margins.Bull; previousBear = margins.Bear;
        }
        return (bands.Select(line => line.Select(v => v.Publish()).ToArray()).ToArray(), trades);
    }
    internal void Reset()
    { _gains.Reset(); _losses.Reset(); foreach (var mean in _smooth) mean.Reset(); _previous = _previousBull = _previousBear = default; _hasPrevious = false; }
    public void Dispose() { _gains.Dispose(); _losses.Dispose(); foreach (var mean in _smooth) mean.Dispose(); }
    private sealed class Average : IDisposable
    {
        private readonly MovingAvgType _kind;
        private readonly int _length;
        private readonly Queue<Number> _history = new();
        private readonly IMovingAverageSmoother? _fallback;
        private Number _sum, _weighted, _previous;
        private long _count;
        internal Average(MovingAvgType kind, int length)
        {
            _kind = kind; _length = Math.Max(1, length);
            if (!StrengthWindow.Supports(kind)) _fallback = MovingAverageSmootherFactory.Create(kind, _length);
        }
        internal Number Next(Number value, bool final)
        {
            if (_fallback is not null) return Number.Of(_fallback.Next(value.Publish(), final));
            if (_length == 1) return value;
            var sum = _sum; var weighted = _weighted; Number result;
            var finite = _kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage;
            if (finite)
            {
                weighted = weighted - sum + value.Times(_length);
                if (_history.Count == _length) sum -= _history.Peek();
                sum += value;
                result = _kind == MovingAvgType.WeightedMovingAverage
                    ? weighted.Divide((long)_length * (_length + 1L) / 2)
                    : _count + 1 < _length ? default : sum.Divide(_length);
            }
            else if (_kind == MovingAvgType.ExponentialMovingAverage && _count < _length)
            { sum += value; result = sum.Divide(_count + 1); }
            else
            {
                var ema = _kind == MovingAvgType.ExponentialMovingAverage;
                result = (_previous.Times(_length - 1L) + value.Times(ema ? 2 : 1))
                    .Divide(ema ? _length + 1L : _length);
            }
            if (final)
            {
                if (finite)
                {
                    if (_history.Count == _length) _history.Dequeue();
                    _history.Enqueue(value);
                }
                _sum = sum; _weighted = weighted; _previous = result; _count++;
            }
            return result;
        }
        internal void Reset() { _history.Clear(); _sum = _weighted = _previous = default; _count = 0; _fallback?.Reset(); }
        public void Dispose() => _fallback?.Dispose();
    }
}

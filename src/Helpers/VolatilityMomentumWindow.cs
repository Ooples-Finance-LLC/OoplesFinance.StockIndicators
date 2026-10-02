using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
using Number = OoplesFinance.StockIndicators.Helpers.PeriodicChannelWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;

// Keep true range, its average and both normalized outputs unpublished until the boundary.
internal sealed class VolatilityMomentumWindow : IDisposable
{
    private readonly int _lag;
    private readonly Average _atr, _signal;
    private readonly Queue<double> _prices = new();
    private double _previous;
    private bool _hasPrevious;
    internal VolatilityMomentumWindow(MovingAvgType kind, int lag, int range)
    { _lag = Math.Max(1, lag); _atr = new(kind, range); _signal = new(kind, _lag); }
    private static Number Abs(Number x) => x.Sign < 0 ? x.Times(-1) : x;
    private static Number TrueRange(double high, double low, double previous)
    {
        var result = Number.Of(high) - Number.Of(low);
        foreach (var gap in new[] { Abs(Number.Of(high) - Number.Of(previous)), Abs(Number.Of(low) - Number.Of(previous)) })
            if ((gap - result).Sign > 0) result = gap;
        return result;
    }
    internal (double Line, double Signal) Next(double high, double low, double close, bool final, bool selected = false)
    {
        StreamingInputValidation.Finite(high, nameof(high)); StreamingInputValidation.Finite(low, nameof(low)); StreamingInputValidation.Finite(close, nameof(close));
        var previous = _hasPrevious ? _previous : close;
        if (selected && !CalculationsHelper.IsWithinBarRange(close, low, high)) { high = Math.Max(close, previous); low = Math.Min(close, previous); }
        var atr = _atr.Next(TrueRange(high, low, previous), final);
        var difference = _prices.Count == _lag ? Number.Of(close) - Number.Of(_prices.Peek()) : default;
        var line = atr.Sign == 0 ? default : difference.Divide(atr);
        var signal = _signal.Next(line, final);
        if (final)
        {
            if (_prices.Count == _lag) _prices.Dequeue();
            _prices.Enqueue(close); _previous = close; _hasPrevious = true;
        }
        return (line.Publish(), signal.Publish());
    }
    private static Signal Trade(Number margin, Number previous)
        => margin.Sign > 0 && (margin - previous).Sign > 0 ? Signal.StrongBuy
            : margin.Sign < 0 && (margin - previous).Sign < 0 ? Signal.StrongSell
            : margin.Sign > 0 ? Signal.Buy : margin.Sign < 0 ? Signal.Sell : Signal.None;
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) Calculate(StockData data, MovingAvgType kind, int lag, int range, string? fastOutput = null)
    {
        lag = Math.Max(1, lag); range = Math.Max(1, range);
        foreach (var values in new[] { data.OpenPrices, data.HighPrices, data.LowPrices, data.ClosePrices, data.Volumes, data.ChainedValues })
            foreach (var value in values) StreamingInputValidation.Finite(value, nameof(data));
        var (prices, highs, lows, _, _) = CalculationsHelper.GetInputValuesList(data);
        var tr = prices.Select((p, i) => TrueRange(highs[i], lows[i], i == 0 ? p : prices[i - 1])).ToArray();
        Number[] Mean(Number[] values, int length, bool callback)
        {
            var supplied = callback ? ComponentAverage.Take(values.Select(v => v.Publish()).ToArray(), length) : null;
            if (supplied is not null) return Enumerable.Range(0, values.Length).Select(i => i < supplied.Count ? Number.Of(supplied[i]) : default).ToArray();
            using var mean = new Average(kind, length); return values.Select(v => mean.Next(v, true)).ToArray();
        }
        var atr = Mean(tr, range, ComponentAverage.HasOverrides);
        var line = prices.Select((p, i) => i < lag || atr[i].Sign == 0 ? default : (Number.Of(p) - Number.Of(prices[i - lag])).Divide(atr[i])).ToArray();
        if (fastOutput == "Vbm") return (new Dictionary<string, double[]> { ["Vbm"] = line.Select(v => v.Publish()).ToArray() }, Array.Empty<Signal>());
        var signal = Mean(line, lag, fastOutput is not null); var trades = new Signal[line.Length]; Number previous = default;
        for (var i = 0; i < line.Length; i++) { var margin = line[i] - signal[i]; trades[i] = Trade(margin, previous); previous = margin; }
        return (new Dictionary<string, double[]> { ["Vbm"] = line.Select(v => v.Publish()).ToArray(), ["Signal"] = signal.Select(v => v.Publish()).ToArray() }, trades);
    }
    internal void Reset() { _prices.Clear(); _atr.Reset(); _signal.Reset(); _previous = 0; _hasPrevious = false; }
    public void Dispose() { _atr.Dispose(); _signal.Dispose(); }
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

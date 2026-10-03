using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class PivotDetectorWindow : IDisposable
{
    private readonly Mean? _level, _gain, _loss;
    private readonly bool _preserveFlat;
    private Number _price, _rsi, _previous, _previousSlope;
    private bool _hasPrevious;
    internal PivotDetectorWindow(MovingAvgType kind, int length1 = 200, int length2 = 14, bool external = false)
    {
        length1 = Math.Max(1, length1); length2 = Math.Max(1, length2);
        if (!external) { _level = new(kind, length1); _gain = new(kind, length2); _loss = new(kind, length2); }
        _preserveFlat = length2 > 1 && kind is MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod;
    }
    internal (double Value, Signal Trade) Next(double price, bool final, double? externalGain = null, double? externalLoss = null, double? externalLevel = null)
    {
        StreamingInputValidation.Finite(price, nameof(price)); var current = Number.Of(price);
        var change = _hasPrevious ? current - _price : default;
        var gain = externalGain.HasValue ? Number.Of(externalGain.Value) : _gain!.Next(change.Sign > 0 ? change : default, final);
        var loss = externalLoss.HasValue ? Number.Of(externalLoss.Value) : _loss!.Next(change.Sign < 0 ? change.Times(-1) : default, final);
        var level = externalLevel.HasValue ? Number.Of(externalLevel.Value) : _level!.Next(current, final);
        var total = gain + loss;
        var rsi = loss.Sign == 0 ? Number.Of(100) : total.Sign == 0 ? default : gain.Times(100).Divide(total);
        if (rsi.Sign < 0) rsi = default; if ((rsi - Number.Of(100)).Sign > 0) rsi = Number.Of(100);
        if (_preserveFlat && _hasPrevious && change.Sign == 0) rsi = _rsi;
        var value = rsi.Times(2) - Number.Of((current - level).Sign > 0 ? 70 : 40);
        var slope = value - _previous; var acceleration = slope - _previousSlope;
        var trade = slope.Sign > 0 && acceleration.Sign > 0 ? Signal.StrongBuy
            : slope.Sign < 0 && acceleration.Sign < 0 ? Signal.StrongSell
            : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) { _price = current; _rsi = rsi; _previous = value; _previousSlope = slope; _hasPrevious = true; }
        return (value.Publish(), trade);
    }
    internal static (double[] Values, Signal[] Trades) Calculate(StockData data, List<double> input, MovingAvgType kind, int length1, int length2, bool callbacks)
    {
        foreach (var price in input) StreamingInputValidation.Finite(price, nameof(input));
        length1 = Math.Max(1, length1); length2 = Math.Max(1, length2);
        var caller = data.CaptureInputSeries();
        try
        {
            var external = callbacks && ComponentAverage.HasOverrides || !StrengthWindow.Supports(kind);
            double[]? gains = null, losses = null, levels = null;
            if (external)
            {
                var changes = input.Select((p, i) => i == 0 ? default(Number) : Number.Of(p) - Number.Of(input[i - 1])).ToArray();
                double[] Average(double[] values, int period)
                {
                    var result = (callbacks ? ComponentAverage.Take(values, period)?.ToArray() : null)
                        ?? CalculationsHelper.GetMovingAverageList(data, kind, period, values.ToList()).ToArray();
                    data.RestoreInputSeries(caller); return result;
                }
                gains = Average(changes.Select(v => v.Sign > 0 ? v.Publish() : 0).ToArray(), length2);
                losses = Average(changes.Select(v => v.Sign < 0 ? v.Times(-1).Publish() : 0).ToArray(), length2);
                levels = Average(input.ToArray(), length1);
            }
            using var window = new PivotDetectorWindow(kind, length1, length2, external);
            var values = new double[input.Count]; var trades = new Signal[input.Count];
            for (var i = 0; i < input.Count; i++) (values[i], trades[i]) = window.Next(input[i], true, gains?[i], losses?[i], levels?[i]);
            return (values, trades);
        }
        finally { data.RestoreInputSeries(caller); }
    }
    private sealed class Mean : IDisposable
    {
        private readonly MovingAvgType _kind; private readonly int _length;
        private readonly Queue<Number> _history = new(); private readonly IMovingAverageSmoother? _fallback;
        private Number _sum, _weighted, _previous; private long _count;
        internal Mean(MovingAvgType kind, int length)
        { _kind = kind; _length = length; if (!StrengthWindow.Supports(kind)) _fallback = MovingAverageSmootherFactory.Create(kind, length); }
        internal Number Next(Number value, bool final)
        {
            if (_fallback is not null) return Number.Of(_fallback.Next(value.Publish(), final));
            var sum = _sum; var weighted = _weighted; Number result;
            var finite = _kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage;
            if (finite)
            {
                weighted = weighted - sum + value.Times(_length);
                if (_history.Count == _length) sum -= _history.Peek(); sum += value;
                result = _kind == MovingAvgType.WeightedMovingAverage ? weighted.Divide((long)_length * (_length + 1L) / 2)
                    : _count + 1 < _length ? default : sum.Divide(_length);
            }
            else if (_kind == MovingAvgType.ExponentialMovingAverage && _count < _length) { sum += value; result = sum.Divide(_count + 1); }
            else result = (_previous.Times(_length - 1L) + value.Times(_kind == MovingAvgType.ExponentialMovingAverage ? 2 : 1)).Divide(_kind == MovingAvgType.ExponentialMovingAverage ? _length + 1L : _length);
            if (final)
            {
                if (finite) { if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(value); }
                _sum = sum; _weighted = weighted; _previous = result; _count++;
            }
            return result;
        }
        internal void Reset() { _history.Clear(); _sum = _weighted = _previous = default; _count = 0; _fallback?.Reset(); }
        public void Dispose() { _fallback?.Dispose(); _history.Clear(); }
    }
    internal void Reset() { _level?.Reset(); _gain?.Reset(); _loss?.Reset(); _price = _rsi = _previous = _previousSlope = default; _hasPrevious = false; }
    public void Dispose() { _level?.Dispose(); _gain?.Dispose(); _loss?.Dispose(); }
}

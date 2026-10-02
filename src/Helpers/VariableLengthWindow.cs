using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class VariableLengthWindow : IDisposable
{
    private readonly int _minimum, _maximum;
    private readonly Average _mean;
    private readonly Queue<Number> _prices = new();
    private Number _sum, _squares, _previous, _previousDifference;
    private int _period; private bool _started;
    internal VariableLengthWindow(MovingAvgType kind, int minimum, int maximum)
    { _minimum = Math.Max(1, minimum); _maximum = Math.Max(_minimum, maximum); _period = _maximum; _mean = new(kind, _maximum); }
    internal (double Value, int Length, Signal Trade) Next(double price, bool final, double? suppliedMean = null)
    {
        StreamingInputValidation.Finite(price, nameof(price)); if (suppliedMean is { } custom) StreamingInputValidation.Finite(custom, nameof(suppliedMean));
        var value = Number.Of(price); var mean = suppliedMean is { } supplied ? Number.Of(supplied) : _mean.Next(value, final);
        var full = _prices.Count == _maximum; var expired = full ? _prices.Peek() : default; var size = full ? _prices.Count : _prices.Count + 1;
        var sum = _sum + value - expired; var squares = _squares + value * value - expired * expired;
        var variance = size < _maximum ? default : (squares.Times(size) - sum * sum).Divide(size).Divide(size);
        var period = _period;
        if (variance.Sign > 0)
        {
            var delta = value - mean; var distance = (delta * delta).Times(16);
            var step = (distance - variance).Sign <= 0 ? 1 : (distance - variance.Times(49)).Sign > 0 ? -1 : 0;
            period = (int)Math.Max(_minimum, Math.Min(_maximum, (long)_period + step));
        }
        var previous = _started ? _previous : value;
        var line = (previous.Times(period - 1L) + value.Times(2)).Divide(period + 1L);
        var difference = value - line; var change = difference - _previousDifference;
        var trade = difference.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : difference.Sign < 0 && change.Sign < 0 ? Signal.StrongSell : difference.Sign > 0 ? Signal.Buy : difference.Sign < 0 ? Signal.Sell : Signal.None;
        if (final)
        {
            if (full) _prices.Dequeue(); _prices.Enqueue(value); _sum = sum; _squares = squares;
            _period = period; _previous = line; _previousDifference = difference; _started = true;
        }
        return (line.Publish(), period, trade);
    }
    internal static (double[] Values, double[] Lengths, Signal[] Signals) Calculate(StockData data, MovingAvgType kind, int minimum, int maximum)
    {
        minimum = Math.Max(1, minimum); maximum = Math.Max(minimum, maximum);
        var prices = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues; foreach (var price in prices) StreamingInputValidation.Finite(price, nameof(prices));
        var custom = ComponentAverage.Take(prices.ToArray(), maximum); using var window = new VariableLengthWindow(kind, minimum, maximum);
        var values = new double[prices.Count]; var lengths = new double[prices.Count]; var signals = new Signal[prices.Count];
        for (var i = 0; i < prices.Count; i++)
        { var point = window.Next(prices[i], true, custom is null ? null : i < custom.Count ? custom[i] : 0); values[i] = point.Value; lengths[i] = point.Length; signals[i] = point.Trade; }
        return (values, lengths, signals);
    }
    private sealed class Average : IDisposable
    {
        private readonly MovingAvgType _kind; private readonly int _length;
        private readonly Queue<Number> _history = new();
        private readonly IMovingAverageSmoother? _fallback;
        private Number _sum, _weighted, _previous; private long _count;
        internal Average(MovingAvgType kind, int length)
        { _kind = kind; _length = Math.Max(1, length); if (!StrengthWindow.Supports(kind)) _fallback = MovingAverageSmootherFactory.Create(kind, _length); }
        internal Number Next(Number value, bool final)
        {
            if (_fallback is not null) return Number.Of(_fallback.Next(value.Publish(), final));
            if (_length == 1) return value;
            var sum = _sum; var weighted = _weighted; Number result;
            var window = _kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage;
            if (window)
            {
                weighted = weighted - sum + value.Times(_length);
                if (_history.Count == _length) sum -= _history.Peek(); sum += value;
                result = _kind == MovingAvgType.WeightedMovingAverage ? weighted.Divide((long)_length * (_length + 1L) / 2)
                    : _count + 1 < _length ? default : sum.Divide(_length);
            }
            else if (_kind == MovingAvgType.ExponentialMovingAverage && _count < _length)
            { sum += value; result = sum.Divide(_count + 1); }
            else
            { var ema = _kind == MovingAvgType.ExponentialMovingAverage; result = (_previous.Times(_length - 1L) + value.Times(ema ? 2 : 1)).Divide(ema ? _length + 1L : _length); }
            if (final) { if (window) { if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(value); } _sum = sum; _weighted = weighted; _previous = result; _count++; }
            return result;
        }
        internal void Reset() { _history.Clear(); _sum = _weighted = _previous = default; _count = 0; _fallback?.Reset(); }
        public void Dispose() => _fallback?.Dispose();
    }
    internal void Reset() { _mean.Reset(); _prices.Clear(); _sum = _squares = _previous = _previousDifference = default; _period = _maximum; _started = false; }
    public void Dispose() { Reset(); _mean.Dispose(); }
}

using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class PhaseChangeWindow : IDisposable
{
    private readonly int _length;
    private readonly Queue<BigInteger> _prices = new();
    private readonly Mean _signal;
    private Number _previousSignal, _previousSlope;
    internal PhaseChangeWindow(MovingAvgType kind, int length, int smooth)
    { _length = Math.Max(2, length); _signal = new(kind, Math.Max(1, smooth)); }
    private Number Raw(double price, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price)); var current = ExactVarianceWindow.Units(price);
        var full = _prices.Count == _length; var start = full ? _prices.Peek() : BigInteger.Zero;
        var change = full ? current - start : BigInteger.Zero;
        BigInteger positive = 0, negative = 0; var lag = _prices.Count;
        // Missing startup bars have zero residual. The common (length-1) denominator cancels in the ratio.
        foreach (var previous in _prices)
        {
            var residual = (previous - start) * (_length - 1L) - change * lag;
            if (residual.Sign > 0) positive += residual; else if (residual.Sign < 0) negative -= residual;
            lag--;
        }
        var total = positive + negative;
        var line = total.IsZero ? default : Number.Integer(100 * positive).Divide(Number.Integer(total));
        if (final) { if (full) _prices.Dequeue(); _prices.Enqueue(current); }
        return line;
    }
    private (double Line, double SignalLine, Signal Trade) Finish(Number line, Number signal, bool final)
    {
        var slope = signal - _previousSignal; var change = slope - _previousSlope;
        // RSI threshold crossings imply the same slope direction; the strong branches take precedence.
        var trade = slope.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : slope.Sign < 0 && change.Sign < 0 ? Signal.StrongSell
            : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) { _previousSignal = signal; _previousSlope = slope; }
        return (line.Publish(), signal.Publish(), trade);
    }
    internal (double Line, double SignalLine, Signal Trade) Next(double price, bool final)
    { var line = Raw(price, final); return Finish(line, _signal.Next(line, final), final); }
    internal static (double[] Line, double[] SignalLine, Signal[] Trades) Calculate(StockData data, List<double> prices,
        MovingAvgType kind, int length, int smooth, bool callbacks, bool includeSignal = true)
    {
        foreach (var price in prices) StreamingInputValidation.Finite(price, nameof(prices));
        var caller = data.CaptureInputSeries();
        try
        {
            using var window = new PhaseChangeWindow(kind, length, smooth);
            var lines = prices.Select(p => window.Raw(p, true)).ToArray();
            if (!includeSignal) return (lines.Select(v => v.Publish()).ToArray(), Array.Empty<double>(), Array.Empty<Signal>());
            smooth = Math.Max(1, smooth);
            var published = callbacks || !StrengthWindow.Supports(kind) ? lines.Select(v => v.Publish()).ToArray() : null;
            var custom = callbacks ? ComponentAverage.Take(published!, smooth) : null;
            var signals = custom is not null ? custom.Select(Number.Of).ToArray()
                : !StrengthWindow.Supports(kind) ? CalculationsHelper.GetMovingAverageList(data, kind, smooth, published!.ToList()).Select(Number.Of).ToArray()
                : lines.Select(v => window._signal.Next(v, true)).ToArray();
            var points = lines.Select((v, i) => window.Finish(v, signals[i], true)).ToArray();
            return (points.Select(v => v.Line).ToArray(), points.Select(v => v.SignalLine).ToArray(), points.Select(v => v.Trade).ToArray());
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
    internal void Reset() { _prices.Clear(); _signal.Reset(); _previousSignal = _previousSlope = default; }
    public void Dispose() { _prices.Clear(); _signal.Dispose(); }
}

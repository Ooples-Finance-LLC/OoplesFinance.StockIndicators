using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class TrendAnalysisIndexWindow : IDisposable
{
    private readonly int _width;
    private readonly Average _mean, _signal;
    private readonly LinkedList<(long Index, Number Value)> _highs = new(), _lows = new();
    private long _index;
    private Number _previousSlope;
    internal TrendAnalysisIndexWindow(MovingAvgType kind, int length1, int length2)
    { _width = Math.Max(1, length2); _mean = new(kind, length1); _signal = new(kind, length2); }
    private Number Extreme(LinkedList<(long Index, Number Value)> deque, Number value, bool maximum, bool final)
    {
        var expiry = _index - _width + 1;
        var first = deque.First; while (first is not null && first.Value.Index < expiry) first = first.Next;
        var result = first is null || (maximum ? (value - first.Value.Value).Sign > 0 : (value - first.Value.Value).Sign < 0) ? value : first.Value.Value;
        if (final)
        {
            while (deque.First is { } old && old.Value.Index < expiry) deque.RemoveFirst();
            while (deque.Last is { } last && (maximum ? (last.Value.Value - value).Sign <= 0 : (last.Value.Value - value).Sign >= 0)) deque.RemoveLast();
            deque.AddLast((_index, value));
        }
        return result;
    }
    private Number RangeRatio(Number price, Number mean, bool final)
    {
        var high = Extreme(_highs, mean, true, final); var low = Extreme(_lows, mean, false, final);
        var line = price.Sign == 0 ? default : (high - low).Times(100).Divide(price);
        if (final) _index++;
        return line;
    }
    private (double Line, double SignalLine, Signal Trade) Finish(Number line, Number signal, Number slope, bool final)
    {
        var acceleration = slope - _previousSlope;
        var trade = (line - signal).Sign < 0 ? Signal.None
            : slope.Sign > 0 && acceleration.Sign > 0 ? Signal.StrongBuy : slope.Sign < 0 && acceleration.Sign < 0 ? Signal.StrongSell
            : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) _previousSlope = slope;
        return (line.Publish(), signal.Publish(), trade);
    }
    internal (double Line, double SignalLine, Signal Trade) Next(double price, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price)); var value = Number.Of(price); var mean = _mean.Next(value, final);
        var line = RangeRatio(value, mean, final); return Finish(line, _signal.Next(line, final), value - mean, final);
    }
    internal static (double[] Line, double[] SignalLine, Signal[] Trades) Calculate(StockData data, MovingAvgType kind, int length1, int length2, bool includeSignal = true)
    {
        length1 = Math.Max(1, length1); length2 = Math.Max(1, length2);
        var prices = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        foreach (var price in prices) StreamingInputValidation.Finite(price, nameof(prices));
        using var window = new TrendAnalysisIndexWindow(kind, length1, length2);
        var means = new Number[prices.Count]; var values = new Number[prices.Count];
        var line = new double[prices.Count]; var signal = new double[prices.Count]; var trades = new Signal[prices.Count];
        var customMean = ComponentAverage.HasOverrides ? ComponentAverage.Take(prices.ToArray(), length1) : null;
        for (var i = 0; i < prices.Count; i++)
        {
            var price = Number.Of(prices[i]); means[i] = customMean is null ? window._mean.Next(price, true) : Number.Of(customMean[i]);
            values[i] = window.RangeRatio(price, means[i], true); line[i] = values[i].Publish();
        }
        var customSignal = includeSignal && ComponentAverage.HasOverrides ? ComponentAverage.Take(line, length2) : null;
        for (var i = 0; i < prices.Count; i++)
        {
            var threshold = !includeSignal ? default : customSignal is null ? window._signal.Next(values[i], true) : Number.Of(customSignal[i]);
            (_, signal[i], trades[i]) = window.Finish(values[i], threshold, Number.Of(prices[i]) - means[i], true);
        }
        return (line, signal, trades);
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
    internal void Reset() { _mean.Reset(); _signal.Reset(); _highs.Clear(); _lows.Clear(); _index = 0; _previousSlope = default; }
    public void Dispose() { Reset(); _mean.Dispose(); _signal.Dispose(); }
}

using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class TurboScalerWindow : IDisposable
{
    private readonly Number _alpha;
    private readonly Average _first, _second, _lineMean, _triggerMean;
    private readonly Range _lineRange, _triggerRange;
    private Number _previousComparison;
    internal TurboScalerWindow(MovingAvgType kind, int length, double alpha)
    {
        StreamingInputValidation.Finite(alpha, nameof(alpha)); _alpha = Number.Of(alpha);
        _first = new(kind, length); _second = new(kind, length); _lineMean = new(kind, length); _triggerMean = new(kind, length);
        _lineRange = new(length); _triggerRange = new(length);
    }
    private Number Blend(Number value, Number mean) => mean + _alpha * (value - mean);
    private (double Line, double Trigger, Signal Trade) Finish(Number line, Number trigger, Number comparison, bool final)
    {
        var change = comparison - _previousComparison;
        var trade = comparison.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : comparison.Sign < 0 && change.Sign < 0 ? Signal.StrongSell
            : comparison.Sign > 0 ? Signal.Buy : comparison.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) _previousComparison = comparison;
        return (line.Publish(), trigger.Publish(), trade);
    }
    internal (double Line, double Trigger, Signal Trade) Next(double price, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price)); var value = Number.Of(price); var first = _first.Next(value, final); var second = _second.Next(first, final);
        var line = _lineRange.Next(value, Blend(value, first), final); var trigger = _triggerRange.Next(first, Blend(first, second), final);
        return Finish(line, trigger, _lineMean.Next(line, final) - _triggerMean.Next(trigger, final), final);
    }
    internal static (double[] Line, double[] Trigger, Signal[] Trades) Calculate(StockData data, MovingAvgType kind, int length, double alpha, bool includeTrades = true)
    {
        length = Math.Max(1, length); using var window = new TurboScalerWindow(kind, length, alpha);
        var prices = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        foreach (var price in prices) StreamingInputValidation.Finite(price, nameof(prices));
        var source = prices.Select(Number.Of).ToArray();
        Number[] Mean(Number[] values)
        {
            var custom = ComponentAverage.Take(values.Select(v => v.Publish()).ToArray(), length);
            if (custom is not null) return Enumerable.Range(0, values.Length).Select(i => i < custom.Count ? Number.Of(custom[i]) : default).ToArray();
            using var average = new Average(kind, length); return values.Select(v => average.Next(v, true)).ToArray();
        }
        // Both public fast outputs historically request these two components.
        var first = Mean(source); var second = Mean(first); var line = new Number[source.Length]; var trigger = new Number[source.Length];
        for (var i = 0; i < source.Length; i++)
        {
            line[i] = window._lineRange.Next(source[i], window.Blend(source[i], first[i]), true);
            trigger[i] = window._triggerRange.Next(first[i], window.Blend(first[i], second[i]), true);
        }
        var lineMean = includeTrades ? Mean(line) : new Number[source.Length]; var triggerMean = includeTrades ? Mean(trigger) : new Number[source.Length]; var trades = new Signal[source.Length];
        if (includeTrades) for (var i = 0; i < source.Length; i++) trades[i] = window.Finish(line[i], trigger[i], lineMean[i] - triggerMean[i], true).Trade;
        return (line.Select(v => v.Publish()).ToArray(), trigger.Select(v => v.Publish()).ToArray(), trades);
    }
    private sealed class Range
    {
        private readonly int _length; private long _index;
        private readonly LinkedList<(long Index, Number Value)> _highs = new(), _lows = new();
        internal Range(int length) => _length = Math.Max(1, length);
        private Number Extreme(LinkedList<(long Index, Number Value)> deque, Number value, bool maximum, bool final)
        {
            var expiry = _index - _length + 1; var first = deque.First;
            while (first is not null && first.Value.Index < expiry) first = first.Next;
            var result = first is null || (maximum ? (value - first.Value.Value).Sign > 0 : (value - first.Value.Value).Sign < 0) ? value : first.Value.Value;
            if (final)
            {
                while (deque.First is { } old && old.Value.Index < expiry) deque.RemoveFirst();
                while (deque.Last is { } last && (maximum ? (last.Value.Value - value).Sign <= 0 : (last.Value.Value - value).Sign >= 0)) deque.RemoveLast();
                deque.AddLast((_index, value));
            }
            return result;
        }
        internal Number Next(Number value, Number blend, bool final)
        {
            var high = Extreme(_highs, blend, true, final); var low = Extreme(_lows, blend, false, final); var width = high - low;
            var result = width.Sign == 0 ? default : (value - low).Divide(width);
            if (final) _index++; return result;
        }
        internal void Reset() { _index = 0; _highs.Clear(); _lows.Clear(); }
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
    internal void Reset() { _first.Reset(); _second.Reset(); _lineMean.Reset(); _triggerMean.Reset(); _lineRange.Reset(); _triggerRange.Reset(); _previousComparison = default; }
    public void Dispose() { Reset(); _first.Dispose(); _second.Dispose(); _lineMean.Dispose(); _triggerMean.Dispose(); }
}

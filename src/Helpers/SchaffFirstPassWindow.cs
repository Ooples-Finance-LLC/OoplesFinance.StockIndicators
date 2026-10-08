using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
// Single-pass Schaff stochastic. The double-pass SHK kernel has a distinct precision policy.
internal sealed class SchaffFirstPassWindow : IDisposable
{
    private readonly int _cycle; private readonly Average? _fast, _slow;
    private readonly LinkedList<(long Index, RocBankValue Value)> _highs = new(), _lows = new(), _scales = new();
    private long _count; private double _previous; private ExactMeanAccumulator _slope;
    internal SchaffFirstPassWindow(MovingAvgType kind, int fast, int slow, int cycle, bool external = false)
    { _cycle = Math.Max(1, cycle); if (!external) { _fast = new(kind, Math.Max(1, fast)); _slow = new(kind, Math.Max(1, slow)); } }
    private static int Compare(RocBankValue left, RocBankValue right)
    { var sum = new ExactMeanAccumulator(); left.AddTo(ref sum); right.AddTo(ref sum, -1); return sum.Sign; }
    private RocBankValue Extreme(LinkedList<(long Index, RocBankValue Value)> deque, RocBankValue value, bool maximum, bool commit)
    {
        var first = deque.First; var threshold = _count - _cycle + 1L; while (first is not null && first.Value.Index < threshold) first = first.Next;
        var result = first is null ? value : (maximum ? Compare(value, first.Value.Value) > 0 : Compare(value, first.Value.Value) < 0) ? value : first.Value.Value;
        if (commit)
        {
            while (deque.First is { } old && old.Value.Index < threshold) deque.RemoveFirst();
            while (deque.Last is { } last && (maximum ? Compare(last.Value.Value, value) <= 0 : Compare(last.Value.Value, value) >= 0)) deque.RemoveLast();
            deque.AddLast((_count, value));
        }
        return result;
    }
    internal (double Value, Signal Signal) Next(double value, bool commit, double? externalFast = null, double? externalSlow = null)
    {
        var fast = externalFast.HasValue ? new RocBankValue(externalFast.Value) : _fast!.Next(new(value), commit); var slow = externalSlow.HasValue ? new RocBankValue(externalSlow.Value) : _slow!.Next(new(value), commit);
        var difference = new ExactMeanAccumulator(); fast.AddTo(ref difference); slow.AddTo(ref difference, -1); var macd = RocBankValue.Round(difference);
        var scaleSum = new ExactMeanAccumulator(); new RocBankValue(Math.Abs(fast.Mantissa), fast.UpperShift).AddTo(ref scaleSum); new RocBankValue(Math.Abs(slow.Mantissa), slow.UpperShift).AddTo(ref scaleSum);
        var scale = Extreme(_scales, RocBankValue.Round(scaleSum), true, commit); var highest = Extreme(_highs, macd, true, commit); var lowest = Extreme(_lows, macd, false, commit);
        var rangeSum = new ExactMeanAccumulator(); highest.AddTo(ref rangeSum); lowest.AddTo(ref rangeSum, -1); var range = RocBankValue.Round(rangeSum);
        var threshold = scale.Multiply(1.4210854715202004e-14); var result = 0d;
        if (Compare(range, threshold) > 0)
        {
            var numerator = new ExactMeanAccumulator(); macd.AddTo(ref numerator, 100); lowest.AddTo(ref numerator, -100); var denominator = new ExactMeanAccumulator(); range.AddTo(ref denominator);
            result = Math.Max(0, Math.Min(100, numerator.Ratio(denominator)));
        }
        var slope = new ExactMeanAccumulator(); slope.Add(result); slope.Add(_previous, -1); var change = slope; change.Subtract(_slope);
        var signal = slope.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : slope.Sign < 0 && change.Sign < 0 ? Signal.StrongSell : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None;
        if (commit) { _count++; _previous = result; _slope = slope; } return (result, signal);
    }
    internal void Reset() { _fast?.Reset(); _slow?.Reset(); _highs.Clear(); _lows.Clear(); _scales.Clear(); _count = 0; _previous = 0; _slope = default; }
    public void Dispose() { _fast?.Dispose(); _slow?.Dispose(); }
    private sealed class Average : IDisposable
    {
        private readonly int _length; private readonly MovingAvgType _kind;
        private readonly Queue<RocBankValue> _history = new(); private ExactMeanAccumulator _sum, _weighted;
        private readonly RocBankAverage? _recursive; private readonly IMovingAverageSmoother? _fallback;
        internal Average(MovingAvgType kind, int length)
        { _kind = kind; _length = length; if (kind is MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod) _recursive = new(kind, length, 1); else if (!StrengthWindow.Supports(kind)) _fallback = MovingAverageSmootherFactory.Create(kind, length); }
        internal RocBankValue Next(RocBankValue value, bool commit)
        {
            if (_recursive is not null) return _recursive.Next(value, commit);
            if (_fallback is not null) return new(_fallback.Next(value.Publish(), commit));
            var sum = _sum; var weighted = _weighted; weighted.Subtract(sum); value.AddTo(ref weighted, _length);
            if (_history.Count == _length) _history.Peek().AddTo(ref sum, -1); value.AddTo(ref sum);
            var result = _kind == MovingAvgType.WeightedMovingAverage ? RocBankValue.Round(weighted, count: (long)_length * (_length + 1L) / 2)
                : _history.Count + 1L < _length ? default : RocBankValue.Round(sum, count: _length);
            if (commit) { _sum = sum; _weighted = weighted; if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(value); } return result;
        }
        internal void Reset() { _sum = _weighted = default; _history.Clear(); _recursive?.Reset(); _fallback?.Reset(); }
        public void Dispose() { _recursive?.Dispose(); _fallback?.Dispose(); }
    }
}

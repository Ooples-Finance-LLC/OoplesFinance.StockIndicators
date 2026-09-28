using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class MovementStrengthWindow : IDisposable
{
    private readonly int _lag, _rangeLength;
    private readonly Queue<double> _prices = new();
    private readonly LinkedList<(long Index, RocBankValue Value)> _high = new(), _low = new();
    private readonly Average _average, _signal;
    private long _count;
    internal MovementStrengthWindow(MovingAvgType kind, int length, int movement, int smoothing)
    { _lag = Math.Max(1, movement) - 1; _rangeLength = Math.Max(2, length); _average = new(kind, Math.Max(1, length)); _signal = new(kind, Math.Max(1, smoothing)); }
    private static int Compare(RocBankValue first, RocBankValue second)
    { var sum = new ExactMeanAccumulator(); first.AddTo(ref sum); second.AddTo(ref sum, -1); return sum.Sign; }
    private RocBankValue Extreme(LinkedList<(long Index, RocBankValue Value)> values, RocBankValue value, bool maximum)
    {
        var node = values.First; while (node is not null && node.Value.Index <= _count - _rangeLength) node = node.Next;
        return node is null || (maximum ? Compare(value, node.Value.Value) >= 0 : Compare(value, node.Value.Value) <= 0) ? value : node.Value.Value;
    }
    private void Commit(LinkedList<(long Index, RocBankValue Value)> values, RocBankValue value, bool maximum)
    {
        while (values.First is not null && values.First.Value.Index <= _count - _rangeLength) values.RemoveFirst();
        while (values.Last is not null && (maximum ? Compare(values.Last.Value.Value, value) <= 0 : Compare(values.Last.Value.Value, value) >= 0)) values.RemoveLast();
        values.AddLast((_count, value));
    }
    internal RocBankValue Movement(double price, bool commit)
    {
        var previous = _lag > 0 && _prices.Count == _lag ? _prices.Peek() : 0; var result = default(RocBankValue);
        if (previous != 0)
        {
            var delta = new ExactMeanAccumulator(); delta.Add(price); delta.Add(previous, -1); var rounded = RocBankValue.Round(delta);
            var first = new ExactMeanAccumulator(); rounded.AddTo(ref first); var averageMove = RocBankValue.Round(first, count: _lag);
            var second = new ExactMeanAccumulator(); averageMove.AddTo(ref second); result = RocBankValue.Round(second, previous);
        }
        if (commit && _lag > 0) { if (_prices.Count == _lag) _prices.Dequeue(); _prices.Enqueue(price); }
        return result;
    }
    internal double Center(RocBankValue value, bool commit)
    {
        var high = Extreme(_high, value, true); var low = Extreme(_low, value, false);
        var delta = new ExactMeanAccumulator(); value.AddTo(ref delta); low.AddTo(ref delta, -1); var range = new ExactMeanAccumulator(); high.AddTo(ref range); low.AddTo(ref range, -1);
        var numerator = new ExactMeanAccumulator(); RocBankValue.Round(delta).AddTo(ref numerator); var denominator = new ExactMeanAccumulator(); RocBankValue.Round(range).AddTo(ref denominator);
        var stochastic = Math.Max(0, Math.Min(100, numerator.Ratio(denominator) * 100)); var result = stochastic * 2 - 100;
        if (commit) { Commit(_high, value, true); Commit(_low, value, false); _count++; }
        return result;
    }
    internal double Next(double price, bool commit)
    { var move = Movement(price, commit); var centered = Center(_average.Next(move, commit), commit); return _signal.Next(new RocBankValue(centered), commit).Publish(); }
    internal void Reset() { _prices.Clear(); _high.Clear(); _low.Clear(); _count = 0; _average.Reset(); _signal.Reset(); }
    public void Dispose() { _average.Dispose(); _signal.Dispose(); }
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

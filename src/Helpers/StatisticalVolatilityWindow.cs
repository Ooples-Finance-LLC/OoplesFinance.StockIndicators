using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class StatisticalVolatilityWindow : IDisposable
{
    private readonly Extreme _closeHigh, _closeLow, _high, _low;
    private readonly Average _signal;
    private readonly double _annual;
    internal StatisticalVolatilityWindow(MovingAvgType kind, int length, int annual)
    {
        length = Math.Max(1, length);
        _closeHigh = new(length, true); _closeLow = new(length, false);
        _high = new(length, true); _low = new(length, false);
        _signal = new(kind, length); _annual = Math.Sqrt((double)Math.Max(1, annual) / length);
    }
    internal double Line(double high, double low, double close, bool commit)
    {
        var closeLog = StableLogRatio.OfSameSign(_closeHigh.Next(close, commit), _closeLow.Next(close, commit));
        var rangeLog = StableLogRatio.OfSameSign(_high.Next(high, commit), _low.Next(low, commit));
        return Math.Max(0, Math.Min(2.99, ((0.6 * closeLog * _annual) + (0.6 * rangeLog * _annual)) * 0.5));
    }
    internal (double Line, double Signal) Next(double high, double low, double close, bool commit)
    { var line = Line(high, low, close, commit); return (line, _signal.Next(new RocBankValue(line), commit).Publish()); }
    internal void Reset() { _closeHigh.Reset(); _closeLow.Reset(); _high.Reset(); _low.Reset(); _signal.Reset(); }
    public void Dispose() { Reset(); _signal.Dispose(); }
    private sealed class Extreme
    {
        private readonly int _length;
        private readonly bool _maximum;
        private readonly LinkedList<(long Index, double Value)> _values = new();
        private long _count;
        internal Extreme(int length, bool maximum) { _length = length; _maximum = maximum; }
        internal double Next(double value, bool commit)
        {
            var first = _values.First;
            while (first is not null && first.Value.Index <= _count - _length) first = first.Next;
            var result = first is null ? value : _maximum ? Math.Max(value, first.Value.Value) : Math.Min(value, first.Value.Value);
            if (commit)
            {
                while (_values.First is not null && _values.First.Value.Index <= _count - _length) _values.RemoveFirst();
                while (_values.Last is not null && (_maximum ? _values.Last.Value.Value <= value : _values.Last.Value.Value >= value)) _values.RemoveLast();
                _values.AddLast((_count++, value));
            }
            return result;
        }
        internal void Reset() { _count = 0; _values.Clear(); }
    }
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

using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class AmDetectorWindow : IDisposable
{
    private readonly int _length;
    private readonly LinkedList<(long Index, BigInteger Value)> _maximum = new();
    private readonly Average _line, _signal;
    private long _count;
    internal AmDetectorWindow(MovingAvgType kind, int length1, int length2)
    { _length = Math.Max(1, length1); length2 = Math.Max(1, length2); _line = new(kind, length2); _signal = new(kind, length2); }
    internal RocBankValue Envelope(double open, double close, bool commit)
    {
        var difference = BigInteger.Abs(ExactVarianceWindow.Units(close) - ExactVarianceWindow.Units(open));
        var node = _maximum.First;
        while (node is not null && node.Value.Index <= _count - _length) node = node.Next;
        var peak = node is null ? difference : BigInteger.Max(difference, node.Value.Value);
        var sum = new ExactMeanAccumulator(); sum.Add(double.Epsilon, peak);
        if (commit)
        {
            while (_maximum.First is not null && _maximum.First.Value.Index <= _count - _length) _maximum.RemoveFirst();
            while (_maximum.Last is not null && _maximum.Last.Value.Value <= difference) _maximum.RemoveLast();
            _maximum.AddLast((_count++, difference));
        }
        return RocBankValue.Round(sum);
    }
    internal (double Line, double Signal) Next(double open, double close, bool commit)
    { var line = _line.Next(Envelope(open, close, commit), commit); var signal = _signal.Next(line, commit); return (line.Publish(), signal.Publish()); }
    internal void Reset() { _maximum.Clear(); _count = 0; _line.Reset(); _signal.Reset(); }
    public void Dispose() { _line.Dispose(); _signal.Dispose(); _maximum.Clear(); }
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

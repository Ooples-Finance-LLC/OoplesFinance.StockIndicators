using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class RunningEquityWindow : IDisposable
{
    private readonly int _length; private readonly Average _average; private readonly Queue<RocBankValue> _changes = new();
    private ExactMeanAccumulator _sum; private double _previous; private int _side; private bool _seeded;
    internal RunningEquityWindow(MovingAvgType kind, int length) { _length = Math.Max(1, length); _average = new(kind, _length); }
    internal double Next(double price, bool commit) => WithAverage(price, _average.Next(new RocBankValue(price), commit).Publish(), commit);
    internal double WithAverage(double price, double average, bool commit)
    {
        var difference = new ExactMeanAccumulator(); if (_seeded) { difference.Add(price); difference.Add(_previous, -1); }
        var change = RocBankValue.Round(difference).Multiply(_side); var sum = _sum;
        if (_changes.Count == _length) _changes.Peek().AddTo(ref sum, -1); change.AddTo(ref sum); var result = sum.Mean(1);
        if (commit) { _sum = sum; if (_changes.Count == _length) _changes.Dequeue(); _changes.Enqueue(change); _previous = price; _side = price.CompareTo(average); _seeded = true; }
        return result;
    }
    internal void Reset() { _average.Reset(); _changes.Clear(); _sum = default; _previous = 0; _side = 0; _seeded = false; }
    public void Dispose() => _average.Dispose();
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

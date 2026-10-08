using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class RecursiveDifferenciatorWindow : IDisposable
{
    private readonly Average _average;
    private readonly PriceRsiWindow _strength;
    private readonly int _length;
    private readonly double _alpha, _retention;
    private readonly Queue<RocBankValue> _values = new();
    private RocBankValue _change;
    private bool _started;
    internal RecursiveDifferenciatorWindow(MovingAvgType kind, int length, double alpha)
    {
        if (double.IsNaN(alpha) || double.IsInfinity(alpha)) throw new ArgumentOutOfRangeException(nameof(alpha));
        _length = Math.Max(1, length); _alpha = alpha; _retention = 1 - alpha;
        _average = new(kind, _length); _strength = new(MovingAvgType.WildersSmoothingMethod, _length);
    }
    internal (double Line, double Change) Next(double price, bool commit)
    {
        var average = _average.Next(new RocBankValue(price), commit).Publish();
        return Finish(_strength.Next(average, commit), commit);
    }
    internal (double Line, double Change) Finish(double strength, bool commit)
    {
        var source = new RocBankValue(strength / 100);
        var previous = _started ? _change : source;
        var sum = new ExactMeanAccumulator(); source.Multiply(_alpha).AddTo(ref sum); previous.Multiply(_retention).AddTo(ref sum);
        var line = RocBankValue.Round(sum);
        var difference = new ExactMeanAccumulator(); line.AddTo(ref difference);
        if (_values.Count == _length) _values.Peek().AddTo(ref difference, -1);
        var change = RocBankValue.Round(difference);
        if (commit)
        { if (_values.Count == _length) _values.Dequeue(); _values.Enqueue(line); _change = change; _started = true; }
        return (line.Publish(), change.Publish());
    }
    internal void Reset() { _average.Reset(); _strength.Reset(); _values.Clear(); _change = default; _started = false; }
    public void Dispose() { _average.Dispose(); _strength.Dispose(); }
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

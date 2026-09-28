using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
// Preserve the exact cumulative total and extended unpublished smoothing stages.
internal sealed class ModifiedObvWindow : IDisposable
{
    private readonly Average _line, _signal;
    private ExactMeanAccumulator _total;
    private double _previous;
    internal ModifiedObvWindow(MovingAvgType kind, int length1, int length2)
    { _line = new(kind, Math.Max(1, length1)); _signal = new(kind, Math.Max(1, length2)); }
    internal (double Line, double Signal) Next(double price, double volume, bool commit)
    {
        var total = _total;
        if (price > _previous) total.Add(volume);
        else if (price < _previous) total.Add(volume, -1);
        var line = _line.Next(RocBankValue.Round(total), commit); var signal = _signal.Next(line, commit);
        if (commit) { _total = total; _previous = price; }
        return (line.Publish(), signal.Publish());
    }
    internal void Reset() { _total = default; _previous = 0; _line.Reset(); _signal.Reset(); }
    public void Dispose() { _line.Dispose(); _signal.Dispose(); }
    // Grow simple/weighted history as observations arrive, including for saturated periods.
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

using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class EhlersSimpleWindow : IDisposable
{
    private readonly Average _first, _second, _third;
    private readonly double _factor;
    private RocBankValue _previous;
    internal EhlersSimpleWindow(MovingAvgType kind, int length)
    { length = Math.Max(1, length); _first = new(kind, length); _second = new(kind, length); _third = new(kind, length); _factor = length / 2.0 * Math.PI; }
    internal static RocBankValue Difference(double close, double open)
    { var sum = new ExactMeanAccumulator(); sum.Add(close); sum.Add(open, -1); return RocBankValue.Round(sum); }
    internal (double Line, double Roc, double Filtered) Finish(RocBankValue first, RocBankValue third, bool commit)
    {
        var change = new ExactMeanAccumulator(); third.AddTo(ref change); _previous.AddTo(ref change, -1);
        var roc = RocBankValue.Round(change).Multiply(_factor); if (commit) _previous = third;
        return (first.Publish(), roc.Publish(), third.Publish());
    }
    internal (double Line, double Roc, double Filtered) Next(double open, double close, bool commit)
    {
        var first = _first.Next(Difference(close, open), commit); var second = _second.Next(first, commit); var third = _third.Next(second, commit);
        return Finish(first, third, commit);
    }
    internal void Reset() { _previous = default; _first.Reset(); _second.Reset(); _third.Reset(); }
    public void Dispose() { _first.Dispose(); _second.Dispose(); _third.Dispose(); }
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

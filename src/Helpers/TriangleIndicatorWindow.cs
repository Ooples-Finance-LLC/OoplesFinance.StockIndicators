using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class TriangleIndicatorWindow : IDisposable
{
    private readonly int _length;
    private readonly long _mass;
    private readonly double _factor;
    private readonly Queue<RocBankValue> _values = new();
    private readonly Average? _average;
    private RocBankValue _previous;
    internal static bool Supports(MovingAvgType kind) => kind == MovingAvgType.EhlersTriangleMovingAverage || StrengthWindow.Supports(kind);
    internal TriangleIndicatorWindow(MovingAvgType kind, int length)
    {
        _length = Math.Max(1, length); var half = (_length + 1L) / 2;
        _mass = half * (_length + 1L - half); _factor = _length / 2.0 * Math.PI;
        if (kind != MovingAvgType.EhlersTriangleMovingAverage) _average = new(kind, _length);
    }
    internal static RocBankValue Difference(double close, double open)
    { var sum = new ExactMeanAccumulator(); sum.Add(close); sum.Add(open, -1); return RocBankValue.Round(sum); }
    internal (double Line, double Roc) Finish(RocBankValue line, bool commit)
    {
        var difference = new ExactMeanAccumulator(); line.AddTo(ref difference); _previous.AddTo(ref difference, -1);
        var roc = RocBankValue.Round(difference).Multiply(_factor);
        if (commit) _previous = line;
        return (line.Publish(), roc.Publish());
    }
    internal (double Line, double Roc) Next(double open, double close, bool commit)
    {
        var value = Difference(close, open); RocBankValue line;
        if (_average is not null) line = _average.Next(value, commit);
        else
        {
            var sum = new ExactMeanAccumulator(); value.AddTo(ref sum);
            var lag = _values.Count;
            foreach (var previous in _values)
            { if (lag < _length) previous.AddTo(ref sum, Math.Min(lag + 1, _length - lag)); lag--; }
            line = RocBankValue.Round(sum, count: _mass);
            if (commit) { if (_values.Count == _length) _values.Dequeue(); _values.Enqueue(value); }
        }
        return Finish(line, commit);
    }
    internal void Reset() { _values.Clear(); _average?.Reset(); _previous = default; }
    public void Dispose() { _average?.Dispose(); _values.Clear(); }
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

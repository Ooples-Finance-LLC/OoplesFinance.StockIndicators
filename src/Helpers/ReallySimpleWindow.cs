using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class ReallySimpleWindow : IDisposable
{
    private readonly Average _price, _signal;
    internal ReallySimpleWindow(MovingAvgType kind, int length, int smoothLength)
    { _price = new(kind, Math.Max(1, length)); _signal = new(kind, Math.Max(1, smoothLength)); }
    internal RocBankValue Line(double close, double low, bool commit, double? externalAverage = null)
    {
        var average = externalAverage.HasValue ? new RocBankValue(externalAverage.Value) : _price.Next(new RocBankValue(close), commit);
        if (close == 0) return default;
        var difference = new ExactMeanAccumulator(); difference.Add(low); average.AddTo(ref difference, -1);
        var delta = RocBankValue.Round(difference); var quotient = new ExactMeanAccumulator(); delta.AddTo(ref quotient);
        return RocBankValue.Round(quotient, close).Multiply(100);
    }
    internal (double Line, double Signal) Next(double close, double low, bool commit)
    { var line = Line(close, low, commit); return (line.Publish(), _signal.Next(line, commit).Publish()); }
    internal void Reset() { _price.Reset(); _signal.Reset(); }
    public void Dispose() { _price.Dispose(); _signal.Dispose(); }
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

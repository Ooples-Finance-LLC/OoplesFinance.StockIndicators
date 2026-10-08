using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class FiniteVolumeWindow : IDisposable
{
    private readonly int _length;
    private readonly double _factor;
    private readonly Average _volume;
    private double _previousTypical;
    private RocBankValue _previous;
    internal FiniteVolumeWindow(MovingAvgType kind, int length, double factor)
    { if (double.IsNaN(factor) || double.IsInfinity(factor)) throw new ArgumentOutOfRangeException(nameof(factor)); _length = Math.Max(1, length); _factor = factor; _volume = new(kind, _length); }
    private static RocBankValue Add(RocBankValue first, RocBankValue second, int sign = 1)
    { var sum = new ExactMeanAccumulator(); first.AddTo(ref sum); second.AddTo(ref sum, sign); return RocBankValue.Round(sum); }
    private static RocBankValue Divide(RocBankValue value, double denominator)
    { var sum = new ExactMeanAccumulator(); value.AddTo(ref sum); return RocBankValue.Round(sum, denominator); }
    private static int Compare(RocBankValue first, RocBankValue second, int sign = -1)
    { var sum = new ExactMeanAccumulator(); first.AddTo(ref sum); second.AddTo(ref sum, sign); return sum.Sign; }
    internal double Next(double high, double low, double close, double volume, bool commit, double? externalVolume = null)
    {
        var average = externalVolume ?? _volume.Next(new RocBankValue(volume), commit).Publish();
        var median = PriceMean.Of(high, low); var typical = PriceMean.Of(high, low, close);
        var flow = Add(Add(Add(new RocBankValue(close), new RocBankValue(median), -1), new RocBankValue(typical)), new RocBankValue(_previousTypical), -1);
        var threshold = Divide(new RocBankValue(close).Multiply(_factor), 100);
        var direction = Compare(flow, threshold) > 0 ? 1 : Compare(flow, threshold, 1) < 0 ? -1 : 0;
        var value = _previous;
        if (average != 0) value = Add(value, Divide(Divide(new RocBankValue(direction * volume), average), _length).Multiply(100));
        if (commit) { _previousTypical = typical; _previous = value; }
        return value.Publish();
    }
    internal void Reset() { _previousTypical = 0; _previous = default; _volume.Reset(); }
    public void Dispose() => _volume.Dispose();
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

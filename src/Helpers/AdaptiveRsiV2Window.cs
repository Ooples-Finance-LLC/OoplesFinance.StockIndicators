using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class AdaptiveRsiV2Window : IDisposable
{
    private readonly int _upper, _lower;
    private readonly AutocorrelationSpectrumWindow _spectrum;
    private readonly EhlersRoofingFilterV2Kernel _roof;
    private readonly List<RocBankValue> _history = new();
    private readonly Average? _average;
    private readonly double _gain, _feedback, _decay;
    private double _ratio, _previous, _older;
    private bool _valid;
    internal AdaptiveRsiV2Window(int upper, int lower, int firstLag, MovingAvgType kind, bool externalAverage = false)
    {
        _upper = Math.Max(1, upper); _lower = Math.Max(1, lower); _spectrum = new(_upper, _lower, Math.Max(1, firstLag)); _roof = new(_upper, _lower); if (!externalAverage) _average = new(kind, _lower);
        var angle = 1.414 * Math.PI / _lower; var radius = Math.Exp(-angle); _feedback = 2 * radius * Math.Cos(Math.Min(angle, .99)); _decay = -radius * radius;
        var gap = 2 * Math.Exp(-angle / 2) * Math.Sinh(angle / 2); var sine = ExactVarianceWindow.Units(Math.Sin(Math.Min(angle, .99) / 2)); var gain = new ExactMeanAccumulator(); gain.AddProduct(gap, gap); var curved = new ExactMeanAccumulator(); curved.Add(-4 * radius, sine * sine); curved.ScaleByPowerOfTwo(-2148); gain.Subtract(curved); _gain = gain.Mean(1);
    }
    internal (double Value, double Average) Next(double price, bool commit)
    {
        var cycle = _spectrum.Next(price, commit).Value; var length = MathHelper.CeilingCycle(MathHelper.MinOrMax(cycle, _upper, _lower) / 2); _roof.Next(price, commit, true); var current = _roof.ExactOutput;
        RocBankValue At(int lag) => lag == 0 ? current : lag <= _history.Count ? _history[_history.Count - lag] : default;
        var rises = new ExactMeanAccumulator(); var total = new ExactMeanAccumulator();
        for (var lag = 0; lag < length && lag <= _history.Count; lag++)
        {
            var change = new ExactMeanAccumulator(); At(lag).AddTo(ref change); At(lag + 1).AddTo(ref change, -1);
            if (change.Sign > 0) { var negative = new ExactMeanAccumulator(); negative.Subtract(change); rises.Subtract(negative); total.Subtract(negative); }
            else total.Subtract(change);
        }
        var valid = !total.IsExactlyZero; var ratio = valid ? rises.Ratio(total) : 0; var value = 0d;
        if (valid && _valid) { var sum = new ExactMeanAccumulator(); sum.AddProduct(ratio, _gain / 2); sum.AddProduct(_ratio, _gain / 2); sum.AddProduct(_previous, _feedback); sum.AddProduct(_older, _decay); value = sum.Mean(1); }
        var average = _average?.Next(new RocBankValue(value), commit).Publish() ?? 0;
        if (commit) { if (_history.Count == _upper) _history.RemoveAt(0); _history.Add(current); _ratio = ratio; _valid = valid; _older = _previous; _previous = value; } return (value, average);
    }
    internal void Reset() { _spectrum.Reset(); _roof.Reset(); _history.Clear(); _average?.Reset(); _ratio = _previous = _older = 0; _valid = false; }
    public void Dispose() { _average?.Dispose(); _history.Clear(); }
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

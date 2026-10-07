using System.Numerics;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class AdaptiveRangeV2Window : IDisposable
{
    private readonly int _upper, _lower;
    private readonly bool _commodity;
    private readonly AutocorrelationSpectrumWindow _spectrum;
    private readonly EhlersRoofingFilterV2Kernel _roof;
    private readonly List<BigInteger> _history = new(), _residuals = new();
    private readonly Average? _average;
    private readonly double _gain, _feedback, _decay;
    private double _ratio, _previous, _older;
    internal AdaptiveRangeV2Window(int upper, int lower, int firstLag, MovingAvgType kind, bool commodity, bool externalAverage = false)
    {
        _upper = Math.Max(1, upper); _lower = Math.Max(1, lower); _commodity = commodity; _spectrum = new(_upper, _lower, Math.Max(1, firstLag)); _roof = new(_upper, _lower); if (!externalAverage) _average = new(kind, _lower);
        var angle = 1.414 * Math.PI / _lower; var radius = Math.Exp(-angle); _feedback = 2 * radius * Math.Cos(Math.Min(angle, .99)); _decay = -radius * radius;
        var gap = 2 * Math.Exp(-angle / 2) * Math.Sinh(angle / 2); var sine = ExactVarianceWindow.Units(Math.Sin(Math.Min(angle, .99) / 2)); var gain = new ExactMeanAccumulator(); gain.AddProduct(gap, gap); var curved = new ExactMeanAccumulator(); curved.Add(-4 * radius, sine * sine); curved.ScaleByPowerOfTwo(-2148); gain.Subtract(curved); _gain = gain.Mean(1);
    }
    internal (double Value, double Average) Next(double price, bool commit)
    {
        var cycle = _spectrum.Next(price, commit).Value; var length = MathHelper.CeilingCycle(MathHelper.MinOrMax(cycle, _upper, _lower)); _roof.Next(price, commit, true); var roof = _roof.ExactOutput; var current = ExactVarianceWindow.Units(roof.Mantissa) << roof.UpperShift;
        var count = Math.Min(length, _history.Count + 1); var residual = BigInteger.Zero; double ratio;
        if (_commodity)
        {
            var total = current; for (var lag = 1; lag < count; lag++) total += _history[_history.Count - lag]; var sum = new ExactMeanAccumulator(); sum.Add(1, total); sum.ScaleByPowerOfTwo(-1074); var mean = RocBankValue.Round(sum, count: count); residual = current - (ExactVarianceWindow.Units(mean.Mantissa) << mean.UpperShift);
            var squares = residual * residual; for (var lag = 1; lag < count; lag++) { var olderResidual = _residuals[_residuals.Count - lag]; squares += olderResidual * olderResidual; }
            var constant = ExactVarianceWindow.Units(.015); ratio = squares.IsZero ? 0 : residual.Sign * ExactPopulationDeviation.RootRatio((residual * residual * count) << 4296, squares * constant * constant);
        }
        else
        {
            var highest = current; var lowest = current; for (var lag = 1; lag < count; lag++) { var value = _history[_history.Count - lag]; highest = BigInteger.Max(highest, value); lowest = BigInteger.Min(lowest, value); }
            var numerator = new ExactMeanAccumulator(); numerator.Add(1, current - lowest); var denominator = new ExactMeanAccumulator(); denominator.Add(1, highest - lowest); ratio = denominator.IsExactlyZero ? 0 : numerator.Ratio(denominator);
        }
        var filtered = new ExactMeanAccumulator(); filtered.AddProduct(ratio, _gain / 2); filtered.AddProduct(_ratio, _gain / 2); filtered.AddProduct(_previous, _feedback); filtered.AddProduct(_older, _decay); var valueNow = filtered.Mean(1); var average = _average?.Next(new RocBankValue(valueNow), commit).Publish() ?? 0;
        if (commit) { if (_history.Count == _upper) { _history.RemoveAt(0); if (_commodity) _residuals.RemoveAt(0); } _history.Add(current); if (_commodity) _residuals.Add(residual); _ratio = ratio; _older = _previous; _previous = valueNow; } return (valueNow, average);
    }
    internal void Reset() { _spectrum.Reset(); _roof.Reset(); _history.Clear(); _residuals.Clear(); _average?.Reset(); _ratio = _previous = _older = 0; }
    public void Dispose() { _average?.Dispose(); _history.Clear(); _residuals.Clear(); }
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

using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class EmpiricalDecompositionWindow : IDisposable
{
    private readonly TrendExtractionWindow _band;
    private readonly Average _trendAverage, _peakAverage, _valleyAverage;
    private Scaled _previous, _older, _peak, _valley;
    internal static bool Supports(MovingAvgType kind) => TrendExtractionWindow.Supports(kind);
    internal EmpiricalDecompositionWindow(MovingAvgType kind, int length, int extremaLength, double delta, double fraction, bool external = false)
    {
        HighLowBandsWindow.ValidateShift(fraction); _band = new(MovingAvgType.SimpleMovingAverage, length, delta); length = Math.Max(1, length); extremaLength = Math.Max(1, extremaLength);
        _trendAverage = new(kind, 2L * length, 1, external); _peakAverage = new(kind, extremaLength, fraction, external); _valleyAverage = new(kind, extremaLength, fraction, external);
    }
    private readonly struct Scaled
    {
        internal readonly double Mantissa; internal readonly int Shift;
        internal Scaled(double mantissa, int shift) { Mantissa = mantissa; Shift = shift; }
        internal void Add(ref ExactMeanAccumulator sum, double coefficient = 1)
        { var negative = new ExactMeanAccumulator(); negative.AddProduct(Mantissa, -coefficient); negative.ScaleByPowerOfTwo(Shift); sum.Subtract(negative); }
        internal void Add(ref ExactMeanAccumulator sum, BigInteger coefficient)
        { var negative = new ExactMeanAccumulator(); negative.Add(Mantissa, -coefficient); negative.ScaleByPowerOfTwo(Shift); sum.Subtract(negative); }
        internal static Scaled Round(ExactMeanAccumulator sum, long divisor = 1)
        {
            if (sum.IsExactlyZero) return default;
            var shift = 0; var value = sum.Mean(divisor); var lower = Math.Pow(2, -256); var upper = Math.Pow(2, 256);
            while (Math.Abs(value) < lower) { sum.ScaleByPowerOfTwo(512); shift -= 512; value = sum.Mean(divisor); }
            while (double.IsInfinity(value) || Math.Abs(value) >= upper) { sum.ScaleByPowerOfTwo(-512); shift += 512; value = sum.Mean(divisor); }
            return new(value, shift);
        }
    }
    private static double Publish(Scaled value) { var sum = new ExactMeanAccumulator(); value.Add(ref sum); return sum.Mean(1); }
    private static int Compare(Scaled left, Scaled right) { var sum = new ExactMeanAccumulator(); left.Add(ref sum); right.Add(ref sum, -1d); return sum.Sign; }
    private sealed class Average : IDisposable
    {
        private readonly long _period, _mass;
        private readonly bool _weightedKind;
        private readonly double _fraction;
        private readonly BigInteger _factor;
        private readonly Queue<Scaled> _history = new();
        private readonly IMovingAverageSmoother? _fallback;
        private ExactMeanAccumulator _sum, _weighted;
        internal Average(MovingAvgType kind, long period, double fraction, bool external)
        {
            _period = period; _mass = period % 2 == 0 ? (period / 2) * (period + 1) : period * ((period + 1) / 2); _weightedKind = kind == MovingAvgType.WeightedMovingAverage; _fraction = fraction; _factor = ExactVarianceWindow.Units(fraction);
            if (!Supports(kind) && !external) _fallback = MovingAverageSmootherFactory.Create(kind, (int)Math.Min(int.MaxValue, period));
        }
        private void Add(ref ExactMeanAccumulator sum, Scaled value, long weight = 1)
        { var term = new ExactMeanAccumulator(); term.Add(value.Mantissa, -_factor * weight); term.ScaleByPowerOfTwo(value.Shift - 1074); sum.Subtract(term); }
        internal double Next(Scaled value, bool commit)
        {
            if (_fallback is not null) return _fraction * _fallback.Next(Publish(value), commit);
            var sum = _sum; var weighted = _weighted; weighted.Subtract(sum); Add(ref weighted, value, _period); Add(ref sum, value);
            if (_history.Count == _period) Add(ref sum, _history.Peek(), -1);
            var output = _weightedKind ? weighted.Mean(_mass) : _history.Count + 1L < _period ? 0 : sum.Mean(_period);
            if (commit) { _sum = sum; _weighted = weighted; if (_history.Count == _period) _history.Dequeue(); _history.Enqueue(value); }
            return output;
        }
        internal void Reset() { _history.Clear(); _sum = _weighted = default; _fallback?.Reset(); }
        public void Dispose() => _fallback?.Dispose();
    }
    private (Scaled Band, Scaled Peak, Scaled Valley) Raw(double price, bool commit)
    {
        var parts = _band.PrepareScaled(price, commit); var band = new Scaled(parts.Mantissa, parts.Shift);
        var peak = Compare(_previous, band) > 0 && Compare(_previous, _older) > 0 ? _previous : _peak;
        var valley = Compare(_previous, band) < 0 && Compare(_previous, _older) < 0 ? _previous : _valley;
        if (commit) { _older = _previous; _previous = band; _peak = peak; _valley = valley; }
        return (band, peak, valley);
    }
    internal (double Band, double Peak, double Valley) Prepare(double price, bool commit)
    { var point = Raw(price, commit); return (Publish(point.Band), Publish(point.Peak), Publish(point.Valley)); }
    internal (double Trend, double Peak, double Valley) Next(double price, bool commit)
    { var point = Raw(price, commit); return (_trendAverage.Next(point.Band, commit), _peakAverage.Next(point.Peak, commit), _valleyAverage.Next(point.Valley, commit)); }
    internal void Reset() { _band.Reset(); _trendAverage.Reset(); _peakAverage.Reset(); _valleyAverage.Reset(); _previous = _older = _peak = _valley = default; }
    public void Dispose() { _band.Dispose(); _trendAverage.Dispose(); _peakAverage.Dispose(); _valleyAverage.Dispose(); }
}

using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class EarlyOnsetWindow
{
    private readonly BigInteger _hpGain, _hpFeedback1, _hpFeedback2;
    private readonly double _c1, _c2, _c3, _k;
    private double _price1, _price2;
    private Scaled _hpPrevious, _hpOlder, _first, _second, _peak;
    internal EarlyOnsetWindow(int low, int high, double k)
    {
        if (double.IsNaN(k) || double.IsInfinity(k)) throw new ArgumentOutOfRangeException(nameof(k)); _k = k;
        var alpha = ExactVarianceWindow.Units(EhlersFirstOrderCoefficient.Alpha(Math.Max(1, high) * Math.Sqrt(2)));
        var unit = BigInteger.One << 1074; var pole = unit - alpha;
        _hpGain = (2 * unit - alpha) * (2 * unit - alpha); _hpFeedback1 = pole << 1077; _hpFeedback2 = -(pole * pole) << 2;
        var angle = Math.Max(.01, Math.Min(.99, Math.Sqrt(2) * Math.PI / Math.Max(1, low))); var radius = Math.Exp(-angle);
        _c2 = 2 * radius * Math.Cos(angle); _c3 = -radius * radius; _c1 = 1 - _c2 - _c3;
    }
    private readonly struct Scaled
    {
        internal readonly double Mantissa; internal readonly int Shift;
        internal Scaled(double mantissa, int shift) { Mantissa = mantissa; Shift = shift; }
        internal void Add(ref ExactMeanAccumulator sum, double coefficient = 1)
        { var negative = new ExactMeanAccumulator(); negative.AddProduct(Mantissa, -coefficient); negative.ScaleByPowerOfTwo(Shift); sum.Subtract(negative); }
        internal void Add(ref ExactMeanAccumulator sum, BigInteger coefficient)
        { var negative = new ExactMeanAccumulator(); negative.Add(Mantissa, -coefficient); negative.ScaleByPowerOfTwo(Shift); sum.Subtract(negative); }
        internal static Scaled Round(ExactMeanAccumulator sum)
        {
            if (sum.IsExactlyZero) return default;
            var shift = 0; var value = sum.Mean(1); var lower = Math.Pow(2, -256); var upper = Math.Pow(2, 256);
            while (Math.Abs(value) < lower) { sum.ScaleByPowerOfTwo(512); shift -= 512; value = sum.Mean(1); }
            while (double.IsInfinity(value) || Math.Abs(value) >= upper) { sum.ScaleByPowerOfTwo(-512); shift += 512; value = sum.Mean(1); }
            return new(value, shift);
        }
    }
    internal double Next(double price, bool commit)
    {
        var hpSum = new ExactMeanAccumulator(); new Scaled(price, 0).Add(ref hpSum, _hpGain); new Scaled(_price1, 0).Add(ref hpSum, -2 * _hpGain); new Scaled(_price2, 0).Add(ref hpSum, _hpGain);
        _hpPrevious.Add(ref hpSum, _hpFeedback1); _hpOlder.Add(ref hpSum, _hpFeedback2); hpSum.ScaleByPowerOfTwo(-2150); var hp = Scaled.Round(hpSum);
        var mean = new ExactMeanAccumulator(); hp.Add(ref mean); _hpPrevious.Add(ref mean); mean.ScaleByPowerOfTwo(-1);
        var filterSum = new ExactMeanAccumulator(); Scaled.Round(mean).Add(ref filterSum, _c1); _first.Add(ref filterSum, _c2); _second.Add(ref filterSum, _c3); var filter = Scaled.Round(filterSum);
        var retained = new ExactMeanAccumulator(); _peak.Add(ref retained, .991); var peak = Scaled.Round(retained);
        var magnitude = new Scaled(Math.Abs(filter.Mantissa), filter.Shift); var comparison = new ExactMeanAccumulator(); magnitude.Add(ref comparison); peak.Add(ref comparison, -1d); if (comparison.Sign > 0) peak = magnitude;
        var top = new ExactMeanAccumulator(); filter.Add(ref top); var bottom = new ExactMeanAccumulator(); peak.Add(ref bottom); var ratio = peak.Mantissa == 0 ? 0 : top.Ratio(bottom);
        var numerator = new ExactMeanAccumulator(); numerator.Add(ratio); numerator.Add(_k); var denominator = new ExactMeanAccumulator(); denominator.Add(1d); denominator.AddProduct(_k, ratio);
        var quotient = denominator.IsExactlyZero ? 0 : numerator.Ratio(denominator);
        if (commit) { _price2 = _price1; _price1 = price; _hpOlder = _hpPrevious; _hpPrevious = hp; _second = _first; _first = filter; _peak = peak; }
        return quotient;
    }
    internal void Reset() { _price1 = _price2 = 0; _hpPrevious = _hpOlder = _first = _second = _peak = default; }
}

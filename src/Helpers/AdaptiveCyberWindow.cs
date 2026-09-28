using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class AdaptiveCyberWindow
{
    private readonly int _length;
    private readonly double _alpha;
    private readonly List<double> _phases = new();
    private readonly double[] _prices = new double[3];
    private readonly Scaled[] _smooth = new Scaled[2], _cycle = new Scaled[6];
    private Scaled _quadrature, _inPhase, _adaptive1, _adaptive2;
    private double _instant, _period;
    private int _startup;
    internal AdaptiveCyberWindow(int length, double alpha)
    {
        if (double.IsNaN(alpha) || double.IsInfinity(alpha)) throw new ArgumentOutOfRangeException(nameof(alpha));
        _length = Math.Max(1, length); _alpha = alpha;
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

    private static Scaled Ratio(ExactMeanAccumulator sum, ExactMeanAccumulator denominator)
    {
        if (sum.IsExactlyZero) return default;
        var shift = 0; var value = sum.Ratio(denominator);
        while (Math.Abs(value) < Math.Pow(2, -256)) { sum.ScaleByPowerOfTwo(512); shift -= 512; value = sum.Ratio(denominator); }
        while (double.IsInfinity(value) || Math.Abs(value) >= Math.Pow(2, 256)) { sum.ScaleByPowerOfTwo(-512); shift += 512; value = sum.Ratio(denominator); }
        return new(value, shift);
    }

    private static Scaled Filter(Scaled current, Scaled previous, Scaled older, Scaled first, Scaled second, double alpha)
    {
        var one = BigInteger.One << 1074; var a = ExactVarianceWindow.Units(alpha);
        var gain = (2 * one - a) * (2 * one - a); var feedback1 = 8 * one * (one - a); var feedback2 = -4 * (one - a) * (one - a);
        var sum = new ExactMeanAccumulator(); current.Add(ref sum, gain); previous.Add(ref sum, -2 * gain); older.Add(ref sum, gain); first.Add(ref sum, feedback1); second.Add(ref sum, feedback2); sum.ScaleByPowerOfTwo(-2150); return Scaled.Round(sum);
    }
    private static void Product(ref ExactMeanAccumulator sum, Scaled left, Scaled right, int sign)
    {
        var negative = new ExactMeanAccumulator(); negative.AddProduct(left.Mantissa, right.Mantissa, -sign); negative.ScaleByPowerOfTwo(left.Shift + right.Shift); sum.Subtract(negative);
    }
    internal (double Cycle, double Period) Next(double price, bool commit)
    {
        var sum = new ExactMeanAccumulator(); sum.Add(price); sum.Add(_prices[0], 2); sum.Add(_prices[1], 2); sum.Add(_prices[2]); var six = new ExactMeanAccumulator(); six.Add(6); var smooth = Ratio(sum, six);
        var startup = new ExactMeanAccumulator(); startup.Add(price); startup.Add(_prices[0], -2); startup.Add(_prices[1]); startup.ScaleByPowerOfTwo(-2); var initial = Scaled.Round(startup);
        var cycle = _startup < 7 ? initial : Filter(smooth, _smooth[0], _smooth[1], _cycle[0], _cycle[1], _alpha);
        var dot = new ExactMeanAccumulator(); cycle.Add(ref dot, .0962); _cycle[1].Add(ref dot, .5769); _cycle[3].Add(ref dot, -.5769); _cycle[5].Add(ref dot, -.0962);
        var quadratureSum = new ExactMeanAccumulator(); Scaled.Round(dot).Add(ref quadratureSum, .5 + .08 * _instant); var quadrature = Scaled.Round(quadratureSum); var inPhase = _cycle[2];
        var advance = .1;
        if (quadrature.Mantissa != 0 && _quadrature.Mantissa != 0)
        {
            var numerator = new ExactMeanAccumulator(); Product(ref numerator, inPhase, _quadrature, 1); Product(ref numerator, _inPhase, quadrature, -1);
            var denominator = new ExactMeanAccumulator(); Product(ref denominator, quadrature, _quadrature, 1); Product(ref denominator, inPhase, _inPhase, 1);
            advance = denominator.IsExactlyZero ? numerator.Sign * Math.Sign(quadrature.Mantissa) * Math.Sign(_quadrature.Mantissa) > 0 ? 1.1 : .1 : Math.Max(.1, Math.Min(1.1, numerator.Ratio(denominator)));
        }
        var ordered = _phases.Skip(_phases.Count == _length ? 1 : 0).Append(advance).OrderBy(v => v).ToArray();
        var middle = ordered.Length / 2; var median = (ordered[(ordered.Length - 1) / 2] + ordered[middle]) / 2;
        var dominant = 6.28318 / median + .5;
        var instantSum = new ExactMeanAccumulator(); instantSum.AddProduct(.33, dominant); instantSum.AddProduct(.67, _instant); var instant = instantSum.Mean(1);
        var periodSum = new ExactMeanAccumulator(); periodSum.AddProduct(.15, instant); periodSum.AddProduct(.85, _period); var period = periodSum.Mean(1);
        var adaptive = _startup < 7 ? initial : Filter(smooth, _smooth[0], _smooth[1], _adaptive1, _adaptive2, 2 / (period + 1));
        var output = new ExactMeanAccumulator(); adaptive.Add(ref output); var published = output.Mean(1);
        if (commit)
        {
            for (var i = _prices.Length - 1; i > 0; i--) _prices[i] = _prices[i - 1]; _prices[0] = price;
            _smooth[1] = _smooth[0]; _smooth[0] = smooth;
            for (var i = _cycle.Length - 1; i > 0; i--) _cycle[i] = _cycle[i - 1]; _cycle[0] = cycle;
            if (_phases.Count == _length) _phases.RemoveAt(0); _phases.Add(advance);
            _quadrature = quadrature; _inPhase = inPhase; _instant = instant; _period = period; _adaptive2 = _adaptive1; _adaptive1 = adaptive; if (_startup < 7) _startup++;
        }
        return (published, period);
    }
    internal void Reset() { Array.Clear(_prices, 0, _prices.Length); Array.Clear(_smooth, 0, _smooth.Length); Array.Clear(_cycle, 0, _cycle.Length); _phases.Clear(); _quadrature = _inPhase = _adaptive1 = _adaptive2 = default; _instant = _period = 0; _startup = 0; }
}

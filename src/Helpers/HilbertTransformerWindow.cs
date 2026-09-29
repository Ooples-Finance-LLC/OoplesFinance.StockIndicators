using System.Numerics;
using OoplesFinance.StockIndicators.Enums;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class HilbertTransformerWindow
{
    private readonly double _pole, _gain, _c1, _c2, _c3;
    private readonly BigInteger _smoothGain, _smoothFirst, _smoothSecond;
    private readonly bool _smooth;
    private double _price1, _price2, _price3, _real1, _real2, _quad1;
    private Scaled _first, _second, _output1, _output2, _peak, _quadPeak, _imag1, _imag2;
    internal HilbertTransformerWindow(int upper, int lower, int smoothing, bool smooth)
    {
        var angle = Math.Min(Math.Sqrt(2) * Math.PI / Math.Max(1, upper), .99);
        _pole = Math.Cos(angle) / (1 + Math.Sin(angle));
        _gain = _pole * _pole / 4;
        var lowAngle = Math.Sqrt(2) * Math.PI / Math.Max(1, lower); var radius = Math.Exp(-lowAngle);
        _c2 = 2 * radius * Math.Cos(Math.Min(lowAngle, .99)); _c3 = -radius * radius;
        var gap = 2 * Math.Exp(-lowAngle / 2) * Math.Sinh(lowAngle / 2); var sine = ExactVarianceWindow.Units(Math.Sin(Math.Min(lowAngle, .99) / 2));
        var gain = new ExactMeanAccumulator(); gain.AddProduct(gap, gap);
        var curved = new ExactMeanAccumulator(); curved.Add(-4 * radius, sine * sine); curved.ScaleByPowerOfTwo(-2148); gain.Subtract(curved); _c1 = gain.Mean(1);
        _smooth = smooth; var smoothAngle = 1.414 * Math.PI / Math.Max(1, smoothing); var smoothRadius = ExactVarianceWindow.Units(Math.Exp(-smoothAngle)); var smoothCosine = ExactVarianceWindow.Units(Math.Cos(smoothAngle));
        _smoothFirst = 2 * smoothRadius * smoothCosine; _smoothSecond = -smoothRadius * smoothRadius; _smoothGain = (BigInteger.One << 2148) - _smoothFirst - _smoothSecond;
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
    private static Scaled Abs(Scaled value) => new(Math.Abs(value.Mantissa), value.Shift);
    private static Scaled Peak(Scaled previous, Scaled current)
    { var decayed = new ExactMeanAccumulator(); previous.Add(ref decayed, .991); var peak = Scaled.Round(decayed); var magnitude = Abs(current); return Compare(peak, magnitude) > 0 ? peak : magnitude; }
    private static double Ratio(Scaled value, Scaled peak)
    { if (peak.Mantissa == 0) return 0; var numerator = new ExactMeanAccumulator(); value.Add(ref numerator); var denominator = new ExactMeanAccumulator(); peak.Add(ref denominator); return numerator.Ratio(denominator); }
    internal (double Real, double Imaginary, Signal Signal) Next(double value, bool commit)
    {
        var numerator = new ExactMeanAccumulator(); numerator.Add(value); numerator.Add(_price1, -1); numerator.Add(_price2, -1); numerator.Add(_price3);
        var drive = Scaled.Round(numerator, 2);
        var firstSum = new ExactMeanAccumulator(); drive.Add(ref firstSum, _gain); _first.Add(ref firstSum, _pole); var first = Scaled.Round(firstSum);
        var secondSum = new ExactMeanAccumulator(); first.Add(ref secondSum); _second.Add(ref secondSum, _pole); var second = Scaled.Round(secondSum);
        var outputSum = new ExactMeanAccumulator(); second.Add(ref outputSum, _c1); _output1.Add(ref outputSum, _c2); _output2.Add(ref outputSum, _c3); var output = Scaled.Round(outputSum);
        var peak = Peak(_peak, output); var real = Ratio(output, peak); var difference = new ExactMeanAccumulator(); difference.Add(real); difference.Add(_real1, -1); var quad = Scaled.Round(difference); var quadPeak = Peak(_quadPeak, quad); var normalized = Ratio(quad, quadPeak);
        Scaled imaginary;
        if (_smooth)
        {
            var smoothSum = new ExactMeanAccumulator(); smoothSum.Add(normalized, _smoothGain); smoothSum.Add(_quad1, _smoothGain); _imag1.Add(ref smoothSum, 2 * _smoothFirst); _imag2.Add(ref smoothSum, 2 * _smoothSecond); smoothSum.ScaleByPowerOfTwo(-2149); imaginary = Scaled.Round(smoothSum);
        }
        else imaginary = new(normalized, 0);
        var current = new ExactMeanAccumulator(); var previous = new ExactMeanAccumulator();
        if (_smooth) { imaginary.Add(ref current); current.Add(normalized, -1); _imag1.Add(ref previous); previous.Add(_quad1, -1); }
        else { current.Add(real); current.Add(_real1, -1); previous.Add(_real1); previous.Add(_real2, -1); }
        var change = current; change.Subtract(previous); var signal = current.Sign > 0 ? change.Sign > 0 ? Signal.StrongBuy : Signal.Buy : current.Sign < 0 ? change.Sign < 0 ? Signal.StrongSell : Signal.Sell : Signal.None;
        if (commit) { _price3 = _price2; _price2 = _price1; _price1 = value; _first = first; _second = second; _output2 = _output1; _output1 = output; _peak = peak; _quadPeak = quadPeak; _real2 = _real1; _real1 = real; _quad1 = normalized; _imag2 = _imag1; _imag1 = imaginary; }
        return (real, Publish(imaginary), signal);
    }
    internal void Reset() { _price1 = _price2 = _price3 = _real1 = _real2 = _quad1 = 0; _first = _second = _output1 = _output2 = _peak = _quadPeak = _imag1 = _imag2 = default; }
}

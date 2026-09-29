using System.Numerics;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class MamaWindow
{
    private readonly double _fast, _slow;
    private readonly double[] _prices = new double[3];
    private readonly Scaled[] _smooth = new Scaled[6], _detrended = new Scaled[6], _quadrature = new Scaled[6], _inphase = new Scaled[6];
    private Scaled _i2, _q2, _real, _imaginary, _mama, _fama;
    private double _period, _smoothPeriod, _phase;
    internal (ExactMeanAccumulator Covariance, ExactMeanAccumulator Energy, ExactMeanAccumulator Distance) NoiseInputs { get; private set; }
    internal MamaWindow(double fast, double slow)
    { HighLowBandsWindow.ValidateShift(fast); HighLowBandsWindow.ValidateShift(slow); _fast = fast; _slow = slow; }
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
    private static Scaled At(Scaled[] history, int lag) => history[lag - 1];
    private static void Shift(Scaled[] history, Scaled value) { for (var i = history.Length - 1; i > 0; i--) history[i] = history[i - 1]; history[0] = value; }
    private static Scaled Fir(Scaled current, Scaled[] history, double correction)
    {
        var sum = new ExactMeanAccumulator(); current.Add(ref sum, .0962); At(history, 2).Add(ref sum, .5769); At(history, 4).Add(ref sum, -.5769); At(history, 6).Add(ref sum, -.0962); var dot = Scaled.Round(sum); var result = new ExactMeanAccumulator(); dot.Add(ref result, correction); return Scaled.Round(result);
    }
    private static void Product(ref ExactMeanAccumulator sum, Scaled left, Scaled right, double coefficient)
    { var negative = new ExactMeanAccumulator(); negative.Add(left.Mantissa, -ExactVarianceWindow.Units(right.Mantissa) * ExactVarianceWindow.Units(coefficient)); negative.ScaleByPowerOfTwo(left.Shift + right.Shift - 2148); sum.Subtract(negative); }
    private static double Blend(double value, double previous, double gain, double retention)
    { var sum = new ExactMeanAccumulator(); sum.AddProduct(value, gain); sum.AddProduct(previous, retention); return sum.Mean(1); }
    private static Scaled Adaptive(Scaled current, Scaled previous, double alpha, bool half)
    { var shift = half ? 1075 : 1074; var weight = ExactVarianceWindow.Units(alpha); var sum = new ExactMeanAccumulator(); current.Add(ref sum, weight); previous.Add(ref sum, (BigInteger.One << shift) - weight); sum.ScaleByPowerOfTwo(-shift); return Scaled.Round(sum); }
    internal (EhlersMamaSnapshot Values, Signal Signal) Next(double price, bool commit, bool includeNoiseInputs = false)
    {
        var mean = new ExactMeanAccumulator(); mean.Add(price, 4); mean.Add(_prices[0], 3); mean.Add(_prices[1], 2); mean.Add(_prices[2]); var smooth = Scaled.Round(mean, 10); var correction = .075 * _period + .54;
        var detrended = Fir(smooth, _smooth, correction); var quadrature = Fir(detrended, _detrended, correction); var inphase = At(_detrended, 3); var ji = Fir(inphase, _inphase, correction); var jq = Fir(quadrature, _quadrature, correction);
        var inSum = new ExactMeanAccumulator(); inphase.Add(ref inSum, .2); jq.Add(ref inSum, -.2); _i2.Add(ref inSum, .8); var i2 = Scaled.Round(inSum);
        var quadSum = new ExactMeanAccumulator(); quadrature.Add(ref quadSum, .2); ji.Add(ref quadSum, .2); _q2.Add(ref quadSum, .8); var q2 = Scaled.Round(quadSum);
        var realSum = new ExactMeanAccumulator(); Product(ref realSum, i2, _i2, .2); Product(ref realSum, q2, _q2, .2); _real.Add(ref realSum, .8); var real = Scaled.Round(realSum);
        var imaginarySum = new ExactMeanAccumulator(); Product(ref imaginarySum, i2, _q2, .2); Product(ref imaginarySum, q2, _i2, -.2); _imaginary.Add(ref imaginarySum, .8); var imaginary = Scaled.Round(imaginarySum);
        var numerator = new ExactMeanAccumulator(); imaginary.Add(ref numerator); var denominator = new ExactMeanAccumulator(); real.Add(ref denominator); var advance = denominator.IsExactlyZero ? 0 : Math.Atan(numerator.Ratio(denominator)); var measured = advance == 0 ? 0 : 2 * Math.PI / advance;
        if (_period != 0) measured = Math.Min(1.5 * _period, Math.Max(.67 * _period, measured)); measured = Math.Min(50, Math.Max(6, measured)); var period = Blend(measured, _period, .2, .8); var smoothPeriod = Blend(period, _smoothPeriod, .33, .67);
        numerator = default; denominator = default; quadrature.Add(ref numerator); inphase.Add(ref denominator); var phase = denominator.IsExactlyZero ? 0 : Math.Atan(numerator.Ratio(denominator)) * (180 / Math.PI); var delta = Math.Max(1, _phase - phase); var alpha = Math.Max(_slow, _fast / delta);
        var mama = Adaptive(new Scaled(price, 0), _mama, alpha, false); var fama = Adaptive(mama, _fama, alpha, true);
        var current = new ExactMeanAccumulator(); mama.Add(ref current); fama.Add(ref current, -1d); var previous = new ExactMeanAccumulator(); _mama.Add(ref previous); _fama.Add(ref previous, -1d); var change = current; change.Subtract(previous); var signal = current.Sign > 0 ? change.Sign > 0 ? Signal.StrongBuy : Signal.Buy : current.Sign < 0 ? change.Sign < 0 ? Signal.StrongSell : Signal.Sell : Signal.None;
        if (includeNoiseInputs)
        {
            var covariance = new ExactMeanAccumulator(); real.Add(ref covariance); imaginary.Add(ref covariance); var energy = new ExactMeanAccumulator(); Product(ref energy, inphase, inphase, 1); Product(ref energy, quadrature, quadrature, 1); var distance = new ExactMeanAccumulator(); distance.Add(price); mama.Add(ref distance, -1d); NoiseInputs = (covariance, energy, distance);
        }
        if (commit) { _prices[2] = _prices[1]; _prices[1] = _prices[0]; _prices[0] = price; Shift(_smooth, smooth); Shift(_detrended, detrended); Shift(_quadrature, quadrature); Shift(_inphase, inphase); _i2 = i2; _q2 = q2; _real = real; _imaginary = imaginary; _period = period; _smoothPeriod = smoothPeriod; _phase = phase; _mama = mama; _fama = fama; }
        return (new EhlersMamaSnapshot(Publish(fama), Publish(mama), Publish(inphase), Publish(quadrature), smoothPeriod, Publish(smooth), Publish(real), Publish(imaginary)), signal);
    }
    internal void Reset() { NoiseInputs = default; Array.Clear(_prices, 0, _prices.Length); foreach (var history in new[] { _smooth, _detrended, _quadrature, _inphase }) Array.Clear(history, 0, history.Length); _i2 = _q2 = _real = _imaginary = _mama = _fama = default; _period = _smoothPeriod = _phase = 0; }
}

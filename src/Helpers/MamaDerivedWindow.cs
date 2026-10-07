using System.Numerics;
using OoplesFinance.StockIndicators.Enums;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class MamaDerivedWindow
{
    private readonly MamaWindow _mama = new(.5, .05);
    private readonly int _mode;
    private readonly List<double> _prices = new(), _smooth = new();
    private readonly List<Scaled> _quadrature = new();
    private Scaled _mean1, _mean2, _mean3, _trend, _real, _imaginary;
    private double _price, _sine, _lead;
    internal MamaDerivedWindow(int mode) => _mode = mode;
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
    private static void Append(List<double> history, double value) { if (history.Count == 52) history.RemoveAt(0); history.Add(value); }
    internal (double First, double Second, Signal Signal) Next(double price, bool commit)
    {
        var mama = _mama.Next(price, commit).Values; var period = mama.SmoothPeriod; var count = Math.Max(1, (int)Math.Ceiling(period + .5));
        var current = new ExactMeanAccumulator(); var previous = new ExactMeanAccumulator(); Scaled mean = default, trend = default, quadrature = default, real = default, imaginary = default; double first, second;
        if (_mode == 0)
        {
            var re = new ExactMeanAccumulator(); var im = new ExactMeanAccumulator(); var scale = new ExactMeanAccumulator();
            for (var lag = 0; lag < count && lag <= _smooth.Count; lag++) { var value = lag == 0 ? mama.Smooth : _smooth[_smooth.Count - lag]; var angle = 2 * Math.PI * ((double)lag / count); re.AddProduct(value, Math.Sin(angle)); im.AddProduct(value, Math.Cos(angle)); scale.Add(Math.Abs(value)); }
            if (!scale.IsExactlyZero) { if (Math.Abs(re.Ratio(scale)) <= 64 * 2.2204460492503131e-16) re = default; if (Math.Abs(im.Ratio(scale)) <= 64 * 2.2204460492503131e-16) im = default; }
            var magnitude = im; if (im.Sign < 0) { magnitude = default; magnitude.Subtract(im); } magnitude.Add(-.001);
            var phase = magnitude.Sign > 0 ? Math.Atan(re.Ratio(im)) * (180 / Math.PI) : 90 * re.Sign; phase += 90; phase += period != 0 ? 360 / period : 0; if (im.Sign < 0) phase += 180; if (phase > 315) phase -= 360;
            first = Math.Sin(phase * (Math.PI / 180)); second = Math.Sin((phase + 45) * (Math.PI / 180)); current.Add(first); current.Add(second, -1); previous.Add(_sine); previous.Add(_lead, -1);
        }
        else if (_mode == 1)
        {
            var difference = new ExactMeanAccumulator(); difference.Add(mama.Smooth); if (_smooth.Count >= 2) difference.Add(_smooth[_smooth.Count - 2], -1); var delta = Scaled.Round(difference); var forcing = new ExactMeanAccumulator(); delta.Add(ref forcing, .5 * (.1759 * period + .4607)); quadrature = Scaled.Round(forcing);
            var realCount = Math.Max(1, (int)Math.Ceiling(period / 2)); var imaginaryCount = Math.Max(1, (int)Math.Ceiling(period / 4)); var re = new ExactMeanAccumulator(); var im = new ExactMeanAccumulator();
            for (var lag = 0; lag < realCount && lag <= _quadrature.Count; lag++) { var value = lag == 0 ? quadrature : _quadrature[_quadrature.Count - lag]; value.Add(ref re, 1.57); if (lag < imaginaryCount) value.Add(ref im, 1.25); }
            real = Scaled.Round(re, realCount); imaginary = Scaled.Round(im, imaginaryCount); first = Publish(real); second = Publish(imaginary); imaginary.Add(ref current); real.Add(ref current, -1d); _imaginary.Add(ref previous); _real.Add(ref previous, -1d);
        }
        else
        {
            var sum = new ExactMeanAccumulator(); sum.Add(price); for (var lag = 1; lag < count && lag <= _prices.Count; lag++) sum.Add(_prices[_prices.Count - lag]); mean = Scaled.Round(sum, count);
            var weighted = new ExactMeanAccumulator(); mean.Add(ref weighted, 4d); _mean1.Add(ref weighted, 3d); _mean2.Add(ref weighted, 2d); _mean3.Add(ref weighted); trend = Scaled.Round(weighted, 10); first = Publish(mean); second = Publish(trend); current.Add(price); trend.Add(ref current, -1d); previous.Add(_price); _trend.Add(ref previous, -1d);
        }
        var change = current; change.Subtract(previous); var signal = current.Sign > 0 ? change.Sign > 0 ? Signal.StrongBuy : Signal.Buy : current.Sign < 0 ? change.Sign < 0 ? Signal.StrongSell : Signal.Sell : Signal.None;
        if (commit) { if (_mode < 2) Append(_smooth, mama.Smooth); else Append(_prices, price); if (_mode == 1) { if (_quadrature.Count == 26) _quadrature.RemoveAt(0); _quadrature.Add(quadrature); } _mean3 = _mean2; _mean2 = _mean1; _mean1 = mean; _trend = trend; _real = real; _imaginary = imaginary; _price = price; _sine = first; _lead = second; }
        return (first, second, signal);
    }
    internal void Reset() { _mama.Reset(); _prices.Clear(); _smooth.Clear(); _quadrature.Clear(); _mean1 = _mean2 = _mean3 = _trend = _real = _imaginary = default; _price = _sine = _lead = 0; }
}

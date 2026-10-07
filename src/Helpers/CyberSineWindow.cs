using System.Numerics;
using OoplesFinance.StockIndicators.Enums;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class CyberSineWindow
{
    private readonly AdaptiveCyberWindow _period;
    private readonly List<Scaled> _history = new();
    private double _price1, _price2, _price3, _sine, _lead;
    private Scaled _smooth1, _smooth2, _cycle1, _cycle2;
    private int _startup;
    internal CyberSineWindow(int length, double alpha) => _period = new(length, alpha);
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
    private static Scaled Filter(Scaled current, Scaled previous, Scaled older, Scaled first, Scaled second, double alpha)
    {
        var one = BigInteger.One << 1074; var a = ExactVarianceWindow.Units(alpha);
        var gain = (2 * one - a) * (2 * one - a); var feedback1 = 8 * one * (one - a); var feedback2 = -4 * (one - a) * (one - a);
        var sum = new ExactMeanAccumulator(); current.Add(ref sum, gain); previous.Add(ref sum, -2 * gain); older.Add(ref sum, gain); first.Add(ref sum, feedback1); second.Add(ref sum, feedback2); sum.ScaleByPowerOfTwo(-2150); return Scaled.Round(sum);
    }
    internal (double Sine, double Lead, Signal Signal) Next(double price, bool commit)
    {
        var period = _period.Next(price, commit).Period; var count = Math.Max(1, MathHelper.CeilingCycle(period));
        var sum = new ExactMeanAccumulator(); sum.Add(price); sum.Add(_price1, 2); sum.Add(_price2, 2); sum.Add(_price3); var smooth = Scaled.Round(sum, 6);
        var initial = new ExactMeanAccumulator(); initial.Add(price); initial.Add(_price1, -2); initial.Add(_price2); initial.ScaleByPowerOfTwo(-2);
        var cycle = _startup < 7 ? Scaled.Round(initial) : Filter(smooth, _smooth1, _smooth2, _cycle1, _cycle2, .07);
        var real = new ExactMeanAccumulator(); var imaginary = new ExactMeanAccumulator(); var scale = new ExactMeanAccumulator();
        for (var lag = 0; lag < count && lag <= _history.Count; lag++)
        {
            var value = lag == 0 ? cycle : _history[_history.Count - lag]; var angle = 2 * Math.PI * ((double)lag / count); value.Add(ref real, Math.Sin(angle)); value.Add(ref imaginary, Math.Cos(angle)); new Scaled(Math.Abs(value.Mantissa), value.Shift).Add(ref scale);
        }
        if (!scale.IsExactlyZero) { if (Math.Abs(real.Ratio(scale)) <= 64 * 2.2204460492503131e-16) real = default; if (Math.Abs(imaginary.Ratio(scale)) <= 64 * 2.2204460492503131e-16) imaginary = default; }
        var magnitude = imaginary; if (imaginary.Sign < 0) { magnitude = default; magnitude.Subtract(imaginary); } magnitude.Add(-.001);
        var phase = magnitude.Sign > 0 ? Math.Atan(real.Ratio(imaginary)) * (180 / Math.PI) : 90 * real.Sign; phase += 90; if (imaginary.Sign < 0) phase += 180; if (phase > 315) phase -= 360;
        var sine = Math.Sin(phase * (Math.PI / 180)); var lead = Math.Sin((phase + 45) * (Math.PI / 180));
        var current = new ExactMeanAccumulator(); current.Add(sine); current.Add(lead, -1); var previous = new ExactMeanAccumulator(); previous.Add(_sine); previous.Add(_lead, -1); var change = current; change.Subtract(previous);
        var signal = current.Sign > 0 ? change.Sign > 0 ? Signal.StrongBuy : Signal.Buy : current.Sign < 0 ? change.Sign < 0 ? Signal.StrongSell : Signal.Sell : Signal.None;
        if (commit) { _price3 = _price2; _price2 = _price1; _price1 = price; _smooth2 = _smooth1; _smooth1 = smooth; _cycle2 = _cycle1; _cycle1 = cycle; _sine = sine; _lead = lead; if (_startup < 7) _startup++; if (_history.Count == 64) _history.RemoveAt(0); _history.Add(cycle); }
        return (sine, lead, signal);
    }
    internal void Reset() { _period.Reset(); _history.Clear(); _price1 = _price2 = _price3 = _sine = _lead = 0; _smooth1 = _smooth2 = _cycle1 = _cycle2 = default; _startup = 0; }
}

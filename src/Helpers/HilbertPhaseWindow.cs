using System.Numerics;
using OoplesFinance.StockIndicators.Enums;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class HilbertPhaseWindow
{
    private readonly int _lag, _horizon;
    private readonly bool _measureCycle;
    private readonly BigInteger _realFirst, _realSecond, _realFeedback, _quadFeedback;
    private readonly Queue<double> _prices = new();
    private readonly List<double> _advances = new();
    private Scaled _lag1, _lag2, _lag3, _lag4, _real1, _real2, _real3, _quad1, _quad2;
    private double _phase, _cycle;
    internal ExactMeanAccumulator NoiseEnergy { get; private set; }
    internal HilbertPhaseWindow(int lag, double realGain, double imaginaryGain, int horizon, bool measureCycle)
    {
        HighLowBandsWindow.ValidateShift(realGain); HighLowBandsWindow.ValidateShift(imaginaryGain); _lag = Math.Max(1, lag); _horizon = Math.Min(360, Math.Max(1, horizon)); _measureCycle = measureCycle;
        var real = ExactVarianceWindow.Units(realGain); var scale = ExactVarianceWindow.Units(1.25); _realFirst = scale << 1074; _realSecond = -scale * real; _realFeedback = real << 1074; _quadFeedback = ExactVarianceWindow.Units(imaginaryGain);
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
    private static void AddSquare(ref ExactMeanAccumulator sum, Scaled component)
    { var opposite = new ExactMeanAccumulator(); opposite.AddProduct(component.Mantissa, -component.Mantissa); opposite.ScaleByPowerOfTwo(2 * component.Shift); sum.Subtract(opposite); }
    private static double Publish(Scaled value) { var sum = new ExactMeanAccumulator(); value.Add(ref sum); return sum.Mean(1); }
    private static Signal SignalFor(Scaled real, Scaled imaginary, Scaled previousReal, Scaled previousImaginary)
    {
        var current = new ExactMeanAccumulator(); real.Add(ref current); imaginary.Add(ref current);
        var previous = new ExactMeanAccumulator(); previousReal.Add(ref previous); previousImaginary.Add(ref previous);
        var change = current; change.Subtract(previous);
        return current.Sign > 0 ? change.Sign > 0 ? Signal.StrongBuy : Signal.Buy : current.Sign < 0 ? change.Sign < 0 ? Signal.StrongSell : Signal.Sell : Signal.None;
    }
    internal (double Real, double Imaginary, double Cycle, Signal Signal) Next(double price, bool commit, bool includeNoiseEnergy = false)
    {
        var differenceSum = new ExactMeanAccumulator(); if (_prices.Count == _lag) { differenceSum.Add(price); differenceSum.Add(_prices.Peek(), -1); } var difference = Scaled.Round(differenceSum);
        var realSum = new ExactMeanAccumulator(); _lag4.Add(ref realSum, _realFirst); _lag2.Add(ref realSum, _realSecond); _real3.Add(ref realSum, _realFeedback); realSum.ScaleByPowerOfTwo(-2148); var real = Scaled.Round(realSum);
        var imaginarySum = new ExactMeanAccumulator(); _lag2.Add(ref imaginarySum, BigInteger.One << 1074); difference.Add(ref imaginarySum, -_quadFeedback); _quad2.Add(ref imaginarySum, _quadFeedback); imaginarySum.ScaleByPowerOfTwo(-1074); var imaginary = Scaled.Round(imaginarySum);
        var phase = _phase; var advance = 0d; var cycle = _cycle;
        if (_measureCycle)
        {
            var numerator = new ExactMeanAccumulator(); imaginary.Add(ref numerator); _quad1.Add(ref numerator);
            var denominator = new ExactMeanAccumulator(); real.Add(ref denominator); _real1.Add(ref denominator);
            phase = denominator.IsExactlyZero ? 0 : (180 / Math.PI) * Math.Atan(Math.Abs(numerator.Ratio(denominator)));
            if (real.Mantissa < 0 && imaginary.Mantissa > 0) phase = 180 - phase;
            if (real.Mantissa < 0 && imaginary.Mantissa < 0) phase = 180 + phase;
            if (real.Mantissa > 0 && imaginary.Mantissa < 0) phase = 360 - phase;
            advance = _phase < 90 && phase > 270 ? 360 + _phase - phase : _phase - phase; advance = Math.Max(1, Math.Min(60, advance));
            var accumulated = advance; var period = 0; for (var lag = 1; lag <= _horizon && lag <= _advances.Count; lag++) { accumulated += _advances[_advances.Count - lag]; if (accumulated > 360) { period = lag; break; } }
            cycle = .25 * period + .75 * _cycle;
        }
        if (includeNoiseEnergy)
        {
            var energy = new ExactMeanAccumulator(); AddSquare(ref energy, real); AddSquare(ref energy, imaginary); NoiseEnergy = energy;
        }
        var signal = SignalFor(real, imaginary, _real1, _quad1);
        if (commit)
        {
            if (_prices.Count == _lag) _prices.Dequeue(); _prices.Enqueue(price); _lag4 = _lag3; _lag3 = _lag2; _lag2 = _lag1; _lag1 = difference;
            _real3 = _real2; _real2 = _real1; _real1 = real; _quad2 = _quad1; _quad1 = imaginary; _phase = phase; _cycle = cycle;
            if (_measureCycle) { if (_advances.Count == _horizon) _advances.RemoveAt(0); _advances.Add(advance); }
        }
        return (Publish(real), Publish(imaginary), cycle, signal);
    }
    internal void Reset() { NoiseEnergy = default; _prices.Clear(); _advances.Clear(); _lag1 = _lag2 = _lag3 = _lag4 = _real1 = _real2 = _real3 = _quad1 = _quad2 = default; _phase = _cycle = 0; }
}

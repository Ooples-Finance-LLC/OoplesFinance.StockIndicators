using System.Numerics;
using OoplesFinance.StockIndicators.Enums;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class SquelchWindow
{
    private readonly int _lag, _threshold, _horizon;
    private readonly Queue<double> _prices = new();
    private readonly Queue<Scaled> _differences = new();
    private readonly List<double> _advances = new();
    private Scaled _lag1, _lag2, _lag3, _lag4, _inphase, _quadrature;
    private double _phase, _cycle;
    internal SquelchWindow(int lag, int threshold, int horizon)
    {
        _lag = Math.Max(1, lag); _threshold = Math.Max(1, threshold);
        // Each advance is at least one degree; the first crossing is at lag <= 360.
        _horizon = Math.Min(360, Math.Max(1, horizon));
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
    private static Signal SignalFor(Scaled real, Scaled imaginary, Scaled previousReal, Scaled previousImaginary)
    {
        var current = new ExactMeanAccumulator(); real.Add(ref current); imaginary.Add(ref current);
        var previous = new ExactMeanAccumulator(); previousReal.Add(ref previous); previousImaginary.Add(ref previous);
        var change = current; change.Subtract(previous);
        return current.Sign > 0 ? change.Sign > 0 ? Signal.StrongBuy : Signal.Buy : current.Sign < 0 ? change.Sign < 0 ? Signal.StrongSell : Signal.Sell : Signal.None;
    }
    internal (double Value, Signal Signal) Next(double price, bool commit)
    {
        var differenceSum = new ExactMeanAccumulator(); if (_prices.Count == _lag) { differenceSum.Add(price); differenceSum.Add(_prices.Peek(), -1); } var difference = Scaled.Round(differenceSum);
        var delayed = _differences.Count == _lag ? _differences.Peek() : default;
        var forcing = new ExactMeanAccumulator(); difference.Add(ref forcing, .75); delayed.Add(ref forcing, -.75); _lag2.Add(ref forcing, .25); _lag4.Add(ref forcing, -.25); var drive = Scaled.Round(forcing);
        var realSum = new ExactMeanAccumulator(); _lag3.Add(ref realSum, .33); _inphase.Add(ref realSum, .67); var real = Scaled.Round(realSum);
        var imaginarySum = new ExactMeanAccumulator(); drive.Add(ref imaginarySum, .2); _quadrature.Add(ref imaginarySum, .8); var imaginary = Scaled.Round(imaginarySum);
        var numerator = new ExactMeanAccumulator(); imaginary.Add(ref numerator); _quadrature.Add(ref numerator);
        var denominator = new ExactMeanAccumulator(); real.Add(ref denominator); _inphase.Add(ref denominator);
        var phase = denominator.IsExactlyZero ? 0 : (180 / Math.PI) * Math.Atan(Math.Abs(numerator.Ratio(denominator)));
        if (real.Mantissa < 0 && imaginary.Mantissa > 0) phase = 180 - phase;
        if (real.Mantissa < 0 && imaginary.Mantissa < 0) phase = 180 + phase;
        if (real.Mantissa > 0 && imaginary.Mantissa < 0) phase = 360 - phase;
        var advance = _phase < 90 && phase > 270 ? 360 + _phase - phase : _phase - phase; advance = Math.Max(1, Math.Min(60, advance));
        var accumulated = advance; var period = 0;
        for (var lag = 1; lag <= _horizon && lag <= _advances.Count; lag++) { accumulated += _advances[_advances.Count - lag]; if (accumulated > 360) { period = lag; break; } }
        var cycle = .25 * period + .75 * _cycle; var signal = SignalFor(real, imaginary, _inphase, _quadrature); var value = cycle < _threshold ? 0d : 1d;
        if (commit)
        {
            if (_prices.Count == _lag) _prices.Dequeue(); _prices.Enqueue(price); if (_differences.Count == _lag) _differences.Dequeue(); _differences.Enqueue(difference);
            _lag4 = _lag3; _lag3 = _lag2; _lag2 = _lag1; _lag1 = difference; _inphase = real; _quadrature = imaginary; _phase = phase; _cycle = cycle;
            if (_advances.Count == _horizon) _advances.RemoveAt(0); _advances.Add(advance);
        }
        return (value, signal);
    }
    internal void Reset() { _prices.Clear(); _differences.Clear(); _advances.Clear(); _lag1 = _lag2 = _lag3 = _lag4 = _inphase = _quadrature = default; _phase = _cycle = 0; }
}

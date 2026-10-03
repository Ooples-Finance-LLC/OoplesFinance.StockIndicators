using System.Numerics;
using OoplesFinance.StockIndicators.Enums;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class MamaNoiseWindow
{
    private readonly MamaWindow _mama = new(.5, .05);
    private readonly int _length, _mode;
    private readonly List<Scaled> _quadrature = new();
    private double _smooth1, _smooth2, _snr;
    private Scaled _range;
    private ExactMeanAccumulator _distance;
    internal MamaNoiseWindow(int length, int mode) { _length = Math.Max(1, length); _mode = mode; }
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
    private static void Square(ref ExactMeanAccumulator sum, Scaled value, double coefficient = 1)
    { var term = new ExactMeanAccumulator(); term.Add(value.Mantissa, ExactVarianceWindow.Units(value.Mantissa) * ExactVarianceWindow.Units(coefficient)); term.ScaleByPowerOfTwo(2 * value.Shift - 2148); var opposite = new ExactMeanAccumulator(); opposite.Subtract(term); sum.Subtract(opposite); }
    private static double LogRatio(ExactMeanAccumulator numerator, ExactMeanAccumulator denominator)
    {
        if (numerator.Sign <= 0 || denominator.Sign <= 0) return 0;
        var shift = 0; var ratio = numerator.Ratio(denominator); var lower = Math.Pow(2, -256); var upper = Math.Pow(2, 256);
        while (ratio < lower) { numerator.ScaleByPowerOfTwo(512); shift -= 512; ratio = numerator.Ratio(denominator); }
        while (double.IsInfinity(ratio) || ratio >= upper) { numerator.ScaleByPowerOfTwo(-512); shift += 512; ratio = numerator.Ratio(denominator); }
        return Math.Log10(ratio) + shift * Math.Log10(2);
    }
    internal (double Snr, double InPhase, double Quadrature, double Period, Signal Signal) Next(double price, double high, double low, bool commit)
    {
        var mama = _mama.Next(price, commit, _mode != 2).Values; var inputs = _mama.NoiseInputs; var numerator = new ExactMeanAccumulator(); var denominator = new ExactMeanAccumulator(); var rangeDifference = new ExactMeanAccumulator(); rangeDifference.Add(high); rangeDifference.Add(low, -1); var difference = Scaled.Round(rangeDifference); var rangeSum = new ExactMeanAccumulator(); Scaled q3 = default, i3 = default;
        if (_mode == 2)
        {
            var change = new ExactMeanAccumulator(); change.Add(mama.Smooth); change.Add(_smooth2, -1); var delta = Scaled.Round(change); var forced = new ExactMeanAccumulator(); delta.Add(ref forced, .5 * (.1759 * mama.SmoothPeriod + .4607)); q3 = Scaled.Round(forced); var count = Math.Max(1, (int)Math.Ceiling(mama.SmoothPeriod / 2)); var sum = new ExactMeanAccumulator(); q3.Add(ref sum, 1.57);
            for (var lag = 1; lag < count && lag <= _quadrature.Count; lag++) _quadrature[_quadrature.Count - lag].Add(ref sum, 1.57); i3 = Scaled.Round(sum, count); Square(ref numerator, i3); Square(ref numerator, q3); Square(ref rangeSum, difference, .025); _range.Add(ref rangeSum, .9);
        }
        else { numerator = _mode == 0 ? inputs.Covariance : inputs.Energy; difference.Add(ref rangeSum, .1); _range.Add(ref rangeSum, .9); }
        var range = Scaled.Round(rangeSum); if (_mode == 2) range.Add(ref denominator); else Square(ref denominator, range); var log = LogRatio(numerator, denominator); var level = _mode == 2 ? 10 * log : 10 * log + _length; var snrSum = new ExactMeanAccumulator(); snrSum.AddProduct(level, _mode == 2 ? .33 : .25); snrSum.AddProduct(_snr, _mode == 2 ? .67 : .75); var snr = _mode == 1 && range.Mantissa <= 0 ? 0 : snrSum.Mean(1);
        var distance = inputs.Distance; if (_mode == 2) { distance = default; distance.Add(price); distance.Add(mama.Smooth, -1); } var acceleration = distance; acceleration.Subtract(_distance); var signal = snr < _length ? Signal.None : distance.Sign > 0 ? acceleration.Sign > 0 ? Signal.StrongBuy : Signal.Buy : distance.Sign < 0 ? acceleration.Sign < 0 ? Signal.StrongSell : Signal.Sell : Signal.None;
        if (commit) { _range = range; _snr = snr; _distance = distance; _smooth2 = _smooth1; _smooth1 = mama.Smooth; if (_mode == 2) { if (_quadrature.Count == 26) _quadrature.RemoveAt(0); _quadrature.Add(q3); } }
        return (snr, Publish(i3), Publish(q3), mama.SmoothPeriod, signal);
    }
    internal void Reset() { _mama.Reset(); _quadrature.Clear(); _smooth1 = _smooth2 = _snr = 0; _range = default; _distance = default; }
}

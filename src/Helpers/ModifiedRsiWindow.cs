using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class ModifiedRsiWindow
{
    private readonly int _length;
    private readonly double _pole;
    private readonly BigInteger _highGain, _gain, _feedback1, _feedback2;
    private readonly Queue<Scaled> _changes = new();
    private ExactMeanAccumulator _gains, _absolute;
    private double _price1, _price2, _price3;
    private Scaled _high1, _high2, _roof1, _roof2, _ratio, _line1, _line2, _signal1, _signal2;
    private bool _hadRatio;
    internal ModifiedRsiWindow(int upper, int lower, int length)
    {
        _length = Math.Max(1, length); upper = Math.Max(1, upper); lower = Math.Max(1, lower);
        var angle = Math.Min(Math.Sqrt(2) * Math.PI / upper, .99); _pole = Math.Cos(angle) / (1 + Math.Sin(angle)); var poleUnits = ExactVarianceWindow.Units(_pole); _highGain = poleUnits * poleUnits;
        var lowAngle = Math.Sqrt(2) * Math.PI / lower; var radius = ExactVarianceWindow.Units(Math.Exp(-lowAngle)); var cosine = ExactVarianceWindow.Units(Math.Cos(Math.Min(lowAngle, .99)));
        _feedback1 = 2 * radius * cosine; _feedback2 = -radius * radius; _gain = (BigInteger.One << 2148) - _feedback1 - _feedback2;
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
    private Scaled Filter(Scaled input, Scaled priorInput, Scaled previous, Scaled older, bool paired)
    {
        var sum = new ExactMeanAccumulator(); input.Add(ref sum, _gain); if (paired) priorInput.Add(ref sum, _gain);
        previous.Add(ref sum, paired ? _feedback1 << 1 : _feedback1); older.Add(ref sum, paired ? _feedback2 << 1 : _feedback2); sum.ScaleByPowerOfTwo(paired ? -2149 : -2148); return Scaled.Round(sum);
    }
    internal (double Line, double Signal) Next(double price, bool commit)
    {
        // Keep the roofing trajectory internal: its published binary64 values can erase the RSI denominator.
        var numerator = new ExactMeanAccumulator(); numerator.Add(price); numerator.Add(_price1, -1); numerator.Add(_price2, -1); numerator.Add(_price3); var drive = Scaled.Round(numerator, 2);
        var firstSum = new ExactMeanAccumulator(); drive.Add(ref firstSum, _highGain); firstSum.ScaleByPowerOfTwo(-2150); _high1.Add(ref firstSum, _pole); var first = Scaled.Round(firstSum);
        var secondSum = new ExactMeanAccumulator(); first.Add(ref secondSum); _high2.Add(ref secondSum, _pole); var second = Scaled.Round(secondSum);
        var roof = Filter(second, default, _roof1, _roof2, false);
        var difference = new ExactMeanAccumulator(); roof.Add(ref difference); _roof1.Add(ref difference, -1d); var change = Scaled.Round(difference);
        var gains = _gains; var absolute = _absolute; if (change.Mantissa > 0) change.Add(ref gains); new Scaled(Math.Abs(change.Mantissa), change.Shift).Add(ref absolute);
        if (_changes.Count == _length) { var expired = _changes.Peek(); if (expired.Mantissa > 0) expired.Add(ref gains, -1d); new Scaled(Math.Abs(expired.Mantissa), expired.Shift).Add(ref absolute, -1d); }
        var valid = !absolute.IsExactlyZero; var ratio = new Scaled(valid ? gains.Ratio(absolute) : 0, 0);
        var line = valid && _hadRatio ? Filter(ratio, _ratio, _line1, _line2, true) : default;
        var signal = Filter(line, _line1, _signal1, _signal2, true);
        if (commit)
        {
            _price3 = _price2; _price2 = _price1; _price1 = price; _high1 = first; _high2 = second; _roof2 = _roof1; _roof1 = roof;
            if (_changes.Count == _length) _changes.Dequeue(); _changes.Enqueue(change); _gains = gains; _absolute = absolute; _ratio = ratio; _hadRatio = valid;
            _line2 = _line1; _line1 = line; _signal2 = _signal1; _signal1 = signal;
        }
        return (Publish(line), Publish(signal));
    }
    internal void Reset() { _changes.Clear(); _gains = _absolute = default; _price1 = _price2 = _price3 = 0; _high1 = _high2 = _roof1 = _roof2 = _ratio = _line1 = _line2 = _signal1 = _signal2 = default; _hadRatio = false; }
}

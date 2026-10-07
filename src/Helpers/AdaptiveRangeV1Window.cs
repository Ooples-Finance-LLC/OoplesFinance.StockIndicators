using System.Numerics;
using OoplesFinance.StockIndicators.Enums;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class AdaptiveRangeV1Window
{
    private readonly MamaWindow _mama = new(.5, .05);
    private readonly double _fraction, _constant;
    private readonly bool _commodity;
    private readonly int _capacity;
    private readonly List<(double Price, double High, double Low)> _history = new();
    private Scaled _average, _older;
    private static int Length(double value) => value <= 0 ? 0 : value >= int.MaxValue - 1d ? int.MaxValue - 1 : (int)Math.Ceiling(value);
    internal AdaptiveRangeV1Window(double fraction, bool commodity, double constant = .015)
    {
        if (double.IsNaN(fraction) || double.IsInfinity(fraction)) throw new ArgumentOutOfRangeException(nameof(fraction));
        if (commodity) CommodityIndexWindow.ValidateConstant(constant);
        _fraction = fraction; _commodity = commodity; _constant = constant; _capacity = Math.Max(1, Length(50 * fraction) + 1);
    }
    private readonly struct Scaled
    {
        internal readonly double Mantissa; internal readonly int Shift;
        internal Scaled(double mantissa, int shift) { Mantissa = mantissa; Shift = shift; }
        internal void Add(ref ExactMeanAccumulator sum, double coefficient = 1)
        { var opposite = new ExactMeanAccumulator(); opposite.AddProduct(Mantissa, -coefficient); opposite.ScaleByPowerOfTwo(Shift); sum.Subtract(opposite); }
        internal static Scaled Ratio(ExactMeanAccumulator numerator, ExactMeanAccumulator denominator)
        {
            if (numerator.IsExactlyZero || denominator.IsExactlyZero) return default;
            var shift = 0; var value = numerator.Ratio(denominator); var lower = Math.Pow(2, -256); var upper = Math.Pow(2, 256);
            while (Math.Abs(value) < lower) { numerator.ScaleByPowerOfTwo(512); shift -= 512; value = numerator.Ratio(denominator); }
            while (double.IsInfinity(value) || Math.Abs(value) >= upper) { numerator.ScaleByPowerOfTwo(-512); shift += 512; value = numerator.Ratio(denominator); }
            return new(value, shift);
        }
        internal double Publish() { var sum = new ExactMeanAccumulator(); Add(ref sum); return sum.Mean(1); }
    }
    internal (double Value, double Average, Signal Signal) Next(double price, double high, double low, double cyclePrice, bool commit)
    {
        var period = _mama.Next(cyclePrice, commit).Values.SmoothPeriod; var length = Length(_fraction * period); var numerator = new ExactMeanAccumulator(); var denominator = new ExactMeanAccumulator();
        if (_commodity)
        {
            length = Math.Max(1, length); var count = Math.Min(length, _history.Count + 1); var n = new BigInteger(length); var current = ExactVarianceWindow.Units(price); var total = current;
            for (var lag = 1; lag < count; lag++) total += ExactVarianceWindow.Units(_history[_history.Count - lag].Price);
            var residual = n * current - total; var deviations = BigInteger.Abs(residual) + (length - count) * BigInteger.Abs(total);
            for (var lag = 1; lag < count; lag++) deviations += BigInteger.Abs(n * ExactVarianceWindow.Units(_history[_history.Count - lag].Price) - total);
            numerator.Add(1d, n * residual); denominator.Add(_constant, deviations);
        }
        else
        {
            var highest = high; var lowest = low;
            for (var lag = 1; lag < length && lag <= _history.Count; lag++) { highest = Math.Max(highest, _history[_history.Count - lag].High); lowest = Math.Min(lowest, _history[_history.Count - lag].Low); }
            if (length > _history.Count + 1) { highest = Math.Max(highest, 0); lowest = Math.Min(lowest, 0); }
            numerator.Add(price, 100); numerator.Add(lowest, -100); denominator.Add(highest); denominator.Add(lowest, -1);
        }
        var value = Scaled.Ratio(numerator, denominator); var alpha = Math.Max(.01, Math.Min(.99, 2d / ((_commodity ? Math.Ceiling(period) : length) + 1))); var sum = new ExactMeanAccumulator(); value.Add(ref sum, alpha); _average.Add(ref sum, 1 - alpha); var one = new ExactMeanAccumulator(); one.Add(1); var average = Scaled.Ratio(sum, one);
        var slope = new ExactMeanAccumulator(); average.Add(ref slope); _average.Add(ref slope, -1); var previous = new ExactMeanAccumulator(); _average.Add(ref previous); _older.Add(ref previous, -1); var acceleration = slope; acceleration.Subtract(previous);
        var lowNow = new ExactMeanAccumulator(); average.Add(ref lowNow); lowNow.Add(_commodity ? 100 : -30); var lowBefore = new ExactMeanAccumulator(); _average.Add(ref lowBefore); lowBefore.Add(_commodity ? 100 : -30); var highNow = new ExactMeanAccumulator(); average.Add(ref highNow); highNow.Add(_commodity ? -100 : -70); var highBefore = new ExactMeanAccumulator(); _average.Add(ref highBefore); highBefore.Add(_commodity ? -100 : -70);
        var signal = slope.Sign > 0 && acceleration.Sign > 0 ? Signal.StrongBuy : slope.Sign < 0 && acceleration.Sign < 0 ? Signal.StrongSell : slope.Sign > 0 || (lowBefore.Sign < 0 && lowNow.Sign > 0) ? Signal.Buy : slope.Sign < 0 || (highBefore.Sign > 0 && highNow.Sign < 0) ? Signal.Sell : Signal.None;
        if (commit) { if (_history.Count == _capacity) _history.RemoveAt(0); _history.Add((price, high, low)); _older = _average; _average = average; }
        return (value.Publish(), average.Publish(), signal);
    }
    internal void Reset() { _mama.Reset(); _history.Clear(); _average = _older = default; }
}

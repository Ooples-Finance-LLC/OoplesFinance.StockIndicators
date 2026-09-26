using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Exact repeated-pole polynomial of the published half-angle coefficient.
internal sealed class HighPassWindow
{
    private readonly BigInteger _gain, _feedback1, _feedback2;
    private RocBankValue _previous, _older;
    private double _input1, _input2;
    internal HighPassWindow(int length, double multiplier = 1)
    {
        if (double.IsNaN(multiplier) || double.IsInfinity(multiplier)) throw new ArgumentOutOfRangeException(nameof(multiplier));
        var alpha = ExactVarianceWindow.Units(EhlersFirstOrderCoefficient.Alpha(multiplier * Math.Max(1, length) * Math.Sqrt(2)));
        var unit = BigInteger.One << 1074; var pole = unit - alpha;
        _gain = (2 * unit - alpha) * (2 * unit - alpha);
        _feedback1 = pole << 1077;
        _feedback2 = -(pole * pole) << 2;
    }
    private static void AddFeedback(ref ExactMeanAccumulator sum, RocBankValue value, BigInteger coefficient)
    {
        var term = new ExactMeanAccumulator(); term.Add(value.Mantissa, coefficient); term.ScaleByPowerOfTwo(value.UpperShift);
        var negative = new ExactMeanAccumulator(); negative.Subtract(term); sum.Subtract(negative);
    }
    internal double Next(double price, bool commit)
    {
        if (double.IsNaN(price) || double.IsInfinity(price)) throw new ArgumentOutOfRangeException(nameof(price));
        var sum = new ExactMeanAccumulator(); sum.Add(price, _gain); sum.Add(_input1, -2 * _gain); sum.Add(_input2, _gain);
        AddFeedback(ref sum, _previous, _feedback1); AddFeedback(ref sum, _older, _feedback2);
        sum.ScaleByPowerOfTwo(-2150);
        var result = RocBankValue.Round(sum);
        if (commit)
        {
            _older = _previous; _previous = result; _input2 = _input1; _input1 = price;
        }
        return result.Publish();
    }
    internal void Reset() { _previous = _older = default; _input1 = _input2 = 0; }
}

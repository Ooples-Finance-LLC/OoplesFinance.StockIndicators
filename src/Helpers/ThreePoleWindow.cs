using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Coefficients retain the exact polynomial of the binary64 radius/cosine.
// In particular the three-pole polynomial never loses its small DC gain.
internal sealed class ThreePoleWindow
{
    private readonly BigInteger _gain, _feedback1, _feedback2, _feedback3;
    private readonly int _binomial, _startup;
    private RocBankValue _previous, _older, _oldest;
    private double _input1, _input2, _input3;
    private int _count;
    internal ThreePoleWindow(int length, int variant)
    {
        length = Math.Max(2, length);
        _binomial = variant == 1 ? 3 : 0;
        _startup = variant == 0 ? 0 : 4;
        var radius = ExactVarianceWindow.Units(Math.Exp(-Math.PI / length));
        var cosine = ExactVarianceWindow.Units(Math.Cos(1.738 * Math.PI / length));
        var pair = 2 * radius * cosine; var real = radius * radius;
        _feedback1 = (pair + real) << 2148;
        _feedback2 = -((real << 2148) + pair * real);
        _feedback3 = real * real;
        _gain = (BigInteger.One << 4296) - _feedback1 - _feedback2 - _feedback3;
    }
    private static void AddFeedback(ref ExactMeanAccumulator sum, RocBankValue value, BigInteger coefficient)
    {
        var term = new ExactMeanAccumulator(); term.Add(value.Mantissa, coefficient); term.ScaleByPowerOfTwo(value.UpperShift);
        var negative = new ExactMeanAccumulator(); negative.Subtract(term); sum.Subtract(negative);
    }
    internal double Next(double price, bool commit)
    {
        if (double.IsNaN(price) || double.IsInfinity(price)) throw new ArgumentOutOfRangeException(nameof(price));
        RocBankValue result;
        if (_count < _startup) result = new(price);
        else
        {
            var sum = new ExactMeanAccumulator(); sum.Add(price, _gain);
            if (_binomial > 0) { sum.Add(_input1, _gain * 3); sum.Add(_input2, _gain * 3); sum.Add(_input3, _gain); }
            AddFeedback(ref sum, _previous, _feedback1 << _binomial);
            AddFeedback(ref sum, _older, _feedback2 << _binomial);
            AddFeedback(ref sum, _oldest, _feedback3 << _binomial);
            sum.ScaleByPowerOfTwo(-4296 - _binomial);
            result = RocBankValue.Round(sum);
        }
        if (commit)
        {
            _oldest = _older; _older = _previous; _previous = result; _input3 = _input2; _input2 = _input1; _input1 = price;
            if (_count < _startup) _count++;
        }
        return result.Publish();
    }
    internal void Reset() { _previous = _older = _oldest = default; _input1 = _input2 = _input3 = 0; _count = 0; }
}

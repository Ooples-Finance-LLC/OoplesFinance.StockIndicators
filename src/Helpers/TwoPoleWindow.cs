using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Coefficients retain the exact polynomial of the binary64 radius/cosine.
// In particular 1-2*r*cos(theta)+r*r never loses its small DC gain.
internal sealed class TwoPoleWindow
{
    private readonly BigInteger _gain, _feedback1, _feedback2;
    private readonly int _binomial, _startup;
    private RocBankValue _previous, _older;
    private double _input1, _input2;
    private int _count;
    internal TwoPoleWindow(int length, int variant)
    {
        length = Math.Max(2, length);
        _binomial = variant == 1 ? 2 : variant == 3 ? 1 : 0;
        _startup = variant is 1 or 2 ? 3 : 0;
        var decay = Math.Sqrt(2) * Math.PI / length;
        var radius = ExactVarianceWindow.Units(Math.Exp(-decay));
        var cosine = ExactVarianceWindow.Units(Math.Cos(variant == 0 ? Math.Sqrt(2) * 1.25 * Math.PI / length : decay));
        _feedback1 = 2 * radius * cosine;
        _feedback2 = -radius * radius;
        _gain = (BigInteger.One << 2148) - _feedback1 - _feedback2;
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
            if (_binomial > 0) sum.Add(_input1, _gain * (_binomial == 2 ? 2 : 1));
            if (_binomial == 2) sum.Add(_input2, _gain);
            AddFeedback(ref sum, _previous, _feedback1 << _binomial);
            AddFeedback(ref sum, _older, _feedback2 << _binomial);
            sum.ScaleByPowerOfTwo(-2148 - _binomial);
            result = RocBankValue.Round(sum);
        }
        if (commit)
        {
            _older = _previous; _previous = result; _input2 = _input1; _input1 = price;
            if (_count < _startup) _count++;
        }
        return result.Publish();
    }
    internal void Reset() { _previous = _older = default; _input1 = _input2 = 0; _count = 0; }
}

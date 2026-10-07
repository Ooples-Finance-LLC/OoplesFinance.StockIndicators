using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Accumulate reciprocal binary inputs exactly; round only the published mean.
internal struct ExactReciprocalSum
{
    private BigInteger _numerator, _denominator;
    private int _count;

    internal void Add(double value)
    {
        if (value == 0) return;
        var units = ExactVarianceWindow.Units(value);
        var magnitude = BigInteger.Abs(units);
        if (_count == 0) { _numerator = units.Sign; _denominator = magnitude; }
        else
        {
            _numerator = _numerator * magnitude + units.Sign * _denominator;
            _denominator *= magnitude;
            var divisor = BigInteger.GreatestCommonDivisor(BigInteger.Abs(_numerator), _denominator);
            _numerator /= divisor;
            _denominator /= divisor;
        }
        _count++;
    }

    internal double Mean => _numerator.IsZero ? 0
        : ExactMeanAccumulator.UnitRatio(_count * _denominator * _numerator.Sign, BigInteger.Abs(_numerator));
}

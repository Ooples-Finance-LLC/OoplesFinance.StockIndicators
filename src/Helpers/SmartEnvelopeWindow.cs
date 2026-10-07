using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class SmartEnvelopeWindow
{
    private readonly BigInteger _divisor, _factor;
    private BigInteger _upper, _lower, _previous;
    private int _upperDirection, _lowerDirection;
    private bool _hasPrevious;
    internal SmartEnvelopeWindow(int length, double factor) { _divisor = new BigInteger(Math.Max(1, length)) << 1074; _factor = ExactVarianceWindow.Units(factor); }
    internal (double Upper, double Middle, double Lower) Next(double close, bool commit)
    {
        var current = ExactVarianceWindow.Units(close); var previousUpper = _hasPrevious ? _upper : current; var previousLower = _hasPrevious ? _lower : current;
        var change = _hasPrevious ? BigInteger.Abs(current - _previous) : BigInteger.Zero;
        var upperDistance = BigInteger.Min(BigInteger.Abs(current - previousUpper), change);
        var lowerDistance = BigInteger.Min(BigInteger.Abs(current - previousLower), change);
        var upper = RocBankValue.RoundUnits(BigInteger.Max(current, previousUpper) * _divisor - upperDistance * _factor * _upperDirection, _divisor);
        var lower = RocBankValue.RoundUnits(BigInteger.Min(current, previousLower) * _divisor + lowerDistance * _factor * _lowerDirection, _divisor);
        var upperDirection = lower < previousLower ? -1 : 1; var lowerDirection = upper > previousUpper ? -1 : 1;
        if (commit) { _upper = upper; _lower = lower; _previous = current; _upperDirection = upperDirection; _lowerDirection = lowerDirection; _hasPrevious = true; }
        return (ExactMeanAccumulator.UnitRatio(upper, BigInteger.One), ExactMeanAccumulator.UnitRatio(upper + lower, new BigInteger(2)), ExactMeanAccumulator.UnitRatio(lower, BigInteger.One));
    }
    internal void Reset() { _upper = _lower = _previous = BigInteger.Zero; _upperDirection = _lowerDirection = 0; _hasPrevious = false; }
}

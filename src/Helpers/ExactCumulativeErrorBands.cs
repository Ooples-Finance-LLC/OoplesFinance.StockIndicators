using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// The published center is rounded by its own average. Retain cumulative absolute
// errors exactly, then round each band once; a hidden error need not fit in double.
internal sealed class ExactCumulativeErrorBands
{
    private readonly BigInteger _multiplier;
    private BigInteger _errorSum, _count;
    internal ExactCumulativeErrorBands(double multiplier) => _multiplier = ExactVarianceWindow.Units(multiplier);
    internal (double Upper, double Lower) Next(double price, double middle, bool commit)
    {
        var center = ExactVarianceWindow.Units(middle);
        var sum = _errorSum + BigInteger.Abs(ExactVarianceWindow.Units(price) - center);
        var n = _count + 1;
        var centerNumerator = (center * n) << 1074;
        var spread = sum * _multiplier;
        var denominator = n << 1074;
        var upper = ExactMeanAccumulator.UnitRatio(centerNumerator + spread, denominator);
        var lower = ExactMeanAccumulator.UnitRatio(centerNumerator - spread, denominator);
        if (commit) { _errorSum = sum; _count = n; }
        return (upper, lower);
    }
    internal void Reset() { _errorSum = default; _count = default; }
}

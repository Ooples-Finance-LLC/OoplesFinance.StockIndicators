using System.Numerics;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Independent sqrt oracle: estimate with the platform sqrt, then certify the
// result against exact adjacent-double midpoints. Production uses integer roots.
internal static class DispersionReferenceArithmetic
{
    internal static double Sqrt(BigInteger numerator, BigInteger denominator)
    {
        if (numerator.Sign < 0 || denominator.Sign <= 0)
            throw new ArgumentOutOfRangeException(nameof(numerator));
        if (numerator.IsZero)
            return 0;
        var exponent = (int)(numerator.GetBitLength() - denominator.GetBitLength());
        if (
            exponent >= 0
                ? numerator < (denominator << exponent)
                : (numerator << -exponent) < denominator
        )
            exponent--;
        var evenExponent = exponent - (exponent & 1);
        var normalized =
            evenExponent >= 0
                ? Round(numerator, denominator << evenExponent)
                : Round(numerator << -evenExponent, denominator);
        var candidate = Math.ScaleB(Math.Sqrt(normalized), evenExponent / 2);
        if (double.IsInfinity(candidate))
            candidate = double.MaxValue;
        var target = 4 * numerator * Grid * Grid;
        while (true)
        {
            var units = Units(candidate);
            var odd = (BitConverter.DoubleToInt64Bits(candidate) & 1) != 0;
            if (candidate > 0)
            {
                var previous = Math.BitDecrement(candidate);
                var midpoint = units + Units(previous);
                var comparison = target.CompareTo(midpoint * midpoint * denominator);
                if (comparison < 0 || comparison == 0 && odd)
                {
                    candidate = previous;
                    continue;
                }
            }
            var next = Math.BitIncrement(candidate);
            var upper = units + (double.IsInfinity(next) ? BigInteger.One << 2098 : Units(next));
            var upperComparison = target.CompareTo(upper * upper * denominator);
            if (upperComparison > 0 || upperComparison == 0 && odd)
            {
                if (double.IsInfinity(next))
                    return next;
                candidate = next;
                continue;
            }
            return candidate;
        }
    }
}

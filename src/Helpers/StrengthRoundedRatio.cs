#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Helpers;

internal static class StrengthRoundedRatio
{
    // Percentage publication supplies |numerator| < 100 * 2^56 and a
    // positive denominator < 2^57. Keep integer conversion residuals exact.
    internal static bool TryDivide(long numerator, long denominator, out double result)
    {
        result = 0;
        const long maximumNumerator = 100L * (1L << 56);
        if (numerator <= -maximumNumerator || numerator >= maximumNumerator
            || denominator <= 0 || denominator >= (1L << 57)) return false;
        if (numerator == 0) return true;
        double top = numerator, bottom = denominator;
        double topTail = numerator - (long)top, bottomTail = denominator - (long)bottom;
        double quotient = top / bottom;
        if (topTail == 0 && bottomTail == 0)
        {
            result = quotient;
            return true;
        }
        // All operands and residuals are far from binary64 overflow/underflow.
        // Each outward step encloses the exact residual n - q*d. A strict
        // interior test rejects ties, including asymmetric power-of-two cells.
        double denominatorLower = Math.BitDecrement(bottom);
        for (int attempt = 0; attempt < 2; attempt++)
        {
            double residual = Math.FusedMultiplyAdd(-quotient, bottom, top);
            double lower = Math.BitDecrement(residual), upper = Math.BitIncrement(residual);
            lower = Math.BitDecrement(Math.FusedMultiplyAdd(-quotient, bottomTail, lower));
            upper = Math.BitIncrement(Math.FusedMultiplyAdd(-quotient, bottomTail, upper));
            lower = Math.BitDecrement(lower + topTail);
            upper = Math.BitIncrement(upper + topTail);
            double lowerCell = Math.BitDecrement((quotient - Math.BitDecrement(quotient)) * .5 * denominatorLower);
            double upperCell = Math.BitDecrement((Math.BitIncrement(quotient) - quotient) * .5 * denominatorLower);
            if (lower > -lowerCell && upper < upperCell)
            {
                result = quotient;
                return true;
            }
            if (lower > 0) quotient = Math.BitIncrement(quotient);
            else if (upper < 0) quotient = Math.BitDecrement(quotient);
            else return false;
        }
        return false;
    }
}
#endif

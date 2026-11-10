using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

internal static class LogVolatilityRoot
{
    internal static double Mean(JurikDyadic numerator, long count)
    {
#if !NETFRAMEWORK
        if (count > 0 && numerator.TryBinaryParts(out var integer, out var exponent)
            && integer >= 0 && TryRoot((UInt128)integer, (ulong)count, exponent, out var result))
            return result;
#endif
        return numerator.SqrtMean(count);
    }

#if !NETFRAMEWORK
    // Normalize and round with the existing exact midpoint certificate.
    // Only the integer-root starting point differs from the shared algorithm.
    internal static bool TryRoot(UInt128 numerator, ulong denominator, int binaryExponent, out double result)
    {
        result = 0;
        if (denominator == 0) return false;
        if (numerator == 0) return true;
        if (numerator <= (1UL << 53) && denominator <= ExactPopulationDeviation.SmallRootMaximumDenominator
            && ExactPopulationDeviation.TrySmallScaledRoot((ulong)numerator, (uint)denominator, binaryExponent, out result)) return true;
        if (numerator <= ulong.MaxValue
            && ExactPopulationDeviation.TryWideScaledRoot((ulong)numerator, denominator, binaryExponent, out result)) return true;

        ulong upper = (ulong)(numerator >> 64);
        int numeratorBits = upper == 0 ? 64 - BitOperations.LeadingZeroCount((ulong)numerator)
            : 128 - BitOperations.LeadingZeroCount(upper);
        int denominatorBits = 64 - BitOperations.LeadingZeroCount(denominator);
        int power = numeratorBits - denominatorBits;
        if (power >= 0 ? numerator < ((UInt128)denominator << power)
            : (numerator << -power) < denominator) power--;
        long exponent = (long)power + binaryExponent;
        long rootExponent = exponent >= 0 ? exponent / 2 : (exponent - 1) / 2;
        if (rootExponent is < -1075 or > 1023) return false;
        int grid = (int)Math.Max(-1074, rootExponent - 52);
        long shift = (long)binaryExponent - 2L * grid;
        UInt128 divisor = denominator;
        if (shift >= 0)
        {
            if (shift >= 128 || numeratorBits + shift > 128) return false;
            numerator <<= (int)shift;
        }
        else
        {
            if (-shift >= 128 || denominatorBits - shift > 128) return false;
            divisor <<= (int)-shift;
        }
        UInt128 root = IntegerRoot(numerator / divisor);
        // root^2 * divisor <= numerator, so the product cannot overflow.
        UInt128 remainder = numerator - root * root * divisor;
        UInt128 boundary = (root << 2) + 1;
        if (remainder > (UInt128.MaxValue >> 2) || divisor > UInt128.MaxValue / boundary) return false;
        int comparison = (remainder << 2).CompareTo(divisor * boundary);
        if (comparison > 0 || comparison == 0 && (root & 1) != 0) root++;
        result = ExactMeanAccumulator.Encode((ulong)root, grid + 1074, false);
        return true;
    }

    // At most 106 input bits leave both neighboring squares inside UInt128.
    // The estimate is never trusted: integer comparisons certify floor(sqrt(n)).
    internal static UInt128 IntegerRoot(UInt128 value)
    {
        if (value >> 106 != 0) return ExactPopulationDeviation.CompactIntegerRoot(value);
        UInt128 root = (ulong)Math.Sqrt((double)value);
        while (root * root > value) root--;
        while ((root + 1) * (root + 1) <= value) root++;
        return root;
    }
#endif
}

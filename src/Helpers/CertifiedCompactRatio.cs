#if !NETFRAMEWORK
using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

internal static class CertifiedCompactRatio
{
    // Certify the division estimate using exact 192-bit midpoint products.
    // Only normal scaled results are accepted: scaling a rounded subnormal
    // would introduce a second rounding. Other cases retain integer division.
    internal static bool TryDivide(UInt128 numerator, UInt128 denominator, int scale, out double result)
    {
        result = 0;
        if (numerator == 0 || denominator == 0) return false;
        long bits = BitConverter.DoubleToInt64Bits((double)numerator / (double)denominator);
        for (int attempt = 0; attempt < 3; attempt++)
        {
            int exponent = (int)(bits >> 52) - 1023;
            long scaledExponent = (long)exponent + scale;
            if (scaledExponent is < -1022 or > 1023) return false;
            ulong significand = (ulong)(bits & 0xfffffffffffffL) | (1UL << 52);
            bool boundary = significand == (1UL << 52);
            ulong lower = boundary ? (significand << 2) - 1 : (significand << 1) - 1;
            if (!Compare(numerator, denominator, lower, exponent - (boundary ? 54 : 53), out int comparison)) return false;
            if (comparison < 0 || comparison == 0 && (bits & 1) != 0) { bits--; continue; }
            if (!Compare(numerator, denominator, (significand << 1) + 1, exponent - 53, out comparison)) return false;
            if (comparison > 0 || comparison == 0 && (bits & 1) != 0) { bits++; continue; }
            result = BitConverter.Int64BitsToDouble(bits + ((long)scale << 52));
            return true;
        }
        return false;
    }

    private static bool Compare(UInt128 numerator, UInt128 denominator, ulong midpoint, int power, out int comparison)
    {
        comparison = 0;
        int shift = -power;
        ulong topHigh = (ulong)(numerator >> 64), topLow = unchecked((ulong)numerator);
        int width = topHigh == 0 ? 64 - BitOperations.LeadingZeroCount(topLow)
            : 128 - BitOperations.LeadingZeroCount(topHigh);
        if (shift is < 0 or >= 192 || width + shift > 192) return false;
        ulong low = 0, middle = 0, high = 0;
        unchecked
        {
            if (shift == 0) { low = topLow; middle = topHigh; }
            else if (shift < 64)
            {
                low = topLow << shift;
                middle = (topHigh << shift) | (topLow >> (64 - shift));
                high = topHigh >> (64 - shift);
            }
            else if (shift == 64) { middle = topLow; high = topHigh; }
            else if (shift < 128)
            {
                middle = topLow << (shift - 64);
                high = (topHigh << (shift - 64)) | (topLow >> (128 - shift));
            }
            else high = topLow << (shift - 128);

            // The denominator is 128 bits and the midpoint at most 54 bits.
            // The upper partial product plus carry fits comfortably in UInt128.
            UInt128 lowerProduct = (UInt128)(ulong)denominator * midpoint;
            UInt128 upperProduct = (denominator >> 64) * midpoint + (lowerProduct >> 64);
            comparison = high.CompareTo((ulong)(upperProduct >> 64));
            if (comparison == 0) comparison = middle.CompareTo((ulong)upperProduct);
            if (comparison == 0) comparison = low.CompareTo((ulong)lowerProduct);
        }
        return true;
    }
}
#endif

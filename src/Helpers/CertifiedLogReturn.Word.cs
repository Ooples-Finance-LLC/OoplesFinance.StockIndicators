#if !NETFRAMEWORK
using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

internal static partial class CertifiedLogReturn
{
    // Normalize the fixed-point scale to the return's magnitude. Endpoints fit
    // a word even for adjacent prices; every product remains an exact UInt128.
    // Nonzero binary exponents and unresolved cells retain the existing paths.
    internal static bool TryWord(ExactMeanAccumulator.ProductFactor current,
        ExactMeanAccumulator.ProductFactor previous, out double value)
    {
        value = 0;
        if (current.Mantissa <= 0 || previous.Mantissa <= 0) return false;
        ulong n = (ulong)current.Mantissa, d = (ulong)previous.Mantissa;
        int exponent = current.Scale - previous.Scale;
        int adjustment = BitOperations.LeadingZeroCount(d) - BitOperations.LeadingZeroCount(n);
        if (adjustment >= 0 ? n < (d << adjustment) : (n << -adjustment) < d) adjustment--;
        if (adjustment < 0) n <<= -adjustment;
        else d <<= adjustment;
        exponent += adjustment;
        if (2 * n >= 3 * d) { d <<= 1; exponent++; }
        if (exponent != 0) return false;
        if (n == d) return true;

        bool negative = n < d;
        ulong numerator = negative ? d - n : n - d, denominator = n + d;
        int power = BitOperations.LeadingZeroCount(denominator) - BitOperations.LeadingZeroCount(numerator);
        if ((numerator << -power) < denominator) power--;
        int bits = 62 - power, upperShift = bits - 64;
        // |atanh argument| < 1/5, with a nonzero numerator and <=55 denominator
        // bits. Thus bits is in [65,117] and the scaled dividend uses <=118 bits.
        if (upperShift is < 1 or > 53) return false;
        var (lower, remainder) = ExactMeanAccumulator.CompactDivRem((UInt128)numerator << bits, denominator);
        if (lower > (UInt128)long.MaxValue) return false;
        ulong low = (ulong)lower, high = low + (remainder == 0 ? 0UL : 1UL);
        ulong mask = (1UL << upperShift) - 1;
        ulong squareLow = WordProduct(low, low, upperShift, mask, false);
        ulong squareHigh = WordProduct(high, high, upperShift, mask, true);
        ulong powerLow = low, powerHigh = high;
        UInt128 sumLow = low, sumHigh = high;
        for (ulong odd = 3; ; odd += 2)
        {
            powerLow = WordProduct(powerLow, squareLow, upperShift, mask, false);
            powerHigh = WordProduct(powerHigh, squareHigh, upperShift, mask, true);
            // squareHigh/2^bits <= 1/16. Once powerHigh <=1, the remaining
            // tail is <1 fixed-point unit: 1/(odd*(1-1/16)) <1 for odd>=3.
            if (powerHigh <= 1) break;
            sumLow += powerLow / odd;
            sumHigh += (powerHigh + odd - 1) / odd;
        }
        double a = WordToDouble(2 * sumLow, bits);
        double b = WordToDouble(2 * (sumHigh + 1), bits);
        if (a != b) return false; // NOSONAR: Certification requires equal rounded endpoints.
        value = negative ? -a : a;
        return true;
    }

    private static ulong WordProduct(ulong left, ulong right, int upperShift, ulong mask, bool ceiling)
    {
        ulong high = Math.BigMul(left, right, out ulong low);
        ulong result = high >> upperShift;
        return result + (ceiling && (low != 0 || (high & mask) != 0) ? 1UL : 0UL);
    }

    private static double WordToDouble(UInt128 value, int bits)
    {
        ulong upper = (ulong)(value >> 64);
        int width = upper == 0 ? 64 - BitOperations.LeadingZeroCount((ulong)value)
            : 128 - BitOperations.LeadingZeroCount(upper);
        int shift = Math.Max(0, width - 53);
        UInt128 rounded = value >> shift;
        if (shift > 0)
        {
            UInt128 half = (UInt128)1 << (shift - 1);
            UInt128 remainder = value & ((half << 1) - 1);
            if (remainder > half || remainder == half && (rounded & 1) != 0) rounded++;
        }
        // The integer significand is exact and scaling stays normal, so this
        // performs the single final binary64 rounding, including midpoint ties.
        return Math.ScaleB((double)(ulong)rounded, shift - bits);
    }
}
#endif

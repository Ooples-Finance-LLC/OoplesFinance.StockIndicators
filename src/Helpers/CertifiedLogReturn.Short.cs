#if !NETFRAMEWORK
using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

internal static partial class CertifiedLogReturn
{
    private static readonly UInt128 ShortLimbMask = ((UInt128)1 << 36) - 1;
    private static readonly (UInt128 Low, UInt128 High) ShortLogTwo = ShortSeries(1, 3);

    // A smaller initial directed interval. Equal rounded endpoints certify the
    // result; unresolved cells retain the unchanged 96-bit and adaptive paths.
    internal static bool TryShort(ExactMeanAccumulator.ProductFactor current,
        ExactMeanAccumulator.ProductFactor previous, out double value)
    {
        value = 0;
        ulong n = (ulong)current.Mantissa, d = (ulong)previous.Mantissa;
        int exponent = current.Scale - previous.Scale;
        if (n == d && exponent == 0) return true;
        int adjustment = BitOperations.LeadingZeroCount(d) - BitOperations.LeadingZeroCount(n);
        if (adjustment >= 0 ? n < (d << adjustment) : (n << -adjustment) < d) adjustment--;
        if (adjustment < 0) n <<= -adjustment;
        else d <<= adjustment;
        exponent += adjustment;
        if (2 * n >= 3 * d) { d <<= 1; exponent++; }
        bool negative = n < d;
        var core = ShortSeries(negative ? d - n : n - d, n + d);
        Int128 lower = negative ? -(Int128)core.High : (Int128)core.Low;
        Int128 upper = negative ? -(Int128)core.Low : (Int128)core.High;
        lower += exponent * (Int128)(exponent < 0 ? ShortLogTwo.High : ShortLogTwo.Low);
        upper += exponent * (Int128)(exponent < 0 ? ShortLogTwo.Low : ShortLogTwo.High);
        var a = ShortToDouble(lower);
        var b = ShortToDouble(upper);
        if (a != b) return false; // NOSONAR: certification requires equal rounded endpoints.
        value = a;
        return true;
    }

    private static (UInt128 Low, UInt128 High) ShortSeries(ulong numerator, ulong denominator)
    {
        if (numerator == 0) return (0, 0);
        // Normalized numerator has at most 55 bits: 55 + 72 <= 127.
        // The full scaled dividend fits UInt128, avoiding the 96-bit path's
        // two staged divisions. CompactDivRem retains its portable fallback.
        UInt128 scaled = (UInt128)numerator << 72;
        var (low, tail) = ExactMeanAccumulator.CompactDivRem(scaled, denominator);
        UInt128 high = low + (tail == 0 ? (UInt128)0 : 1);
        UInt128 squareLow = ShortProduct(low, low, false);
        UInt128 squareHigh = ShortProduct(high, high, true);
        // The first odd denominator is one, so both directed terms are the
        // endpoints themselves. Start with them instead of dividing twice.
        UInt128 powerLow = low, powerHigh = high, sumLow = low, sumHigh = high;
        for (uint odd = 1; ; odd += 2)
        {
            powerLow = ShortProduct(powerLow, squareLow, false);
            powerHigh = ShortProduct(powerHigh, squareHigh, true);
            // |atanh argument| <= 1/3, including log(2). Once powerHigh <= 1,
            // the remaining geometric tail is less than one fixed-point unit:
            // 1 / ((odd + 2) * (1 - squareHigh / 2^72)) < 1.
            if (powerHigh <= 1) return (2 * sumLow, 2 * (sumHigh + 1));
            uint nextOdd = odd + 2;
            sumLow += powerLow / nextOdd;
            sumHigh += (powerHigh + nextOdd - 1) / nextOdd;
        }
    }

    private static UInt128 ShortProduct(UInt128 left, UInt128 right, bool ceiling)
    {
        UInt128 leftLow = left & ShortLimbMask, leftHigh = left >> 36;
        UInt128 rightLow = right & ShortLimbMask, rightHigh = right >> 36;
        UInt128 low = leftLow * rightLow;
        UInt128 cross = leftLow * rightHigh + leftHigh * rightLow;
        UInt128 carry = (low >> 36) + (cross & ShortLimbMask);
        UInt128 result = leftHigh * rightHigh + (cross >> 36) + (carry >> 36);
        bool remainder = (low & ShortLimbMask) != 0 || (carry & ShortLimbMask) != 0;
        return result + (ceiling && remainder ? (UInt128)1 : 0);
    }

    private static double ShortToDouble(Int128 value)
    {
        bool negative = value < 0;
        UInt128 magnitude = (UInt128)(negative ? -value : value);
        ulong upper = (ulong)(magnitude >> 64);
        int bits = upper == 0 ? 64 - BitOperations.LeadingZeroCount((ulong)magnitude)
            : 128 - BitOperations.LeadingZeroCount(upper);
        int shift = Math.Max(0, bits - 53);
        UInt128 rounded = magnitude >> shift;
        if (shift > 0)
        {
            UInt128 half = (UInt128)1 << (shift - 1);
            UInt128 remainder = magnitude & ((half << 1) - 1);
            if (remainder > half || remainder == half && (rounded & 1) != 0) rounded++;
        }
        double result = Math.ScaleB((double)rounded, shift - 72);
        return negative ? -result : result;
    }
}
#endif

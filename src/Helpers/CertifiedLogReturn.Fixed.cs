#if !NETFRAMEWORK
using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

internal static partial class CertifiedLogReturn
{
    private static readonly UInt128 LimbMask = ((UInt128)1 << 48) - 1;
    private static readonly (UInt128 Low, UInt128 High) FixedLogTwo = FixedSeries(1, 3);

    // Exactly the same 96-bit directed atanh interval as the arbitrary-precision
    // path. Two 48-bit limbs suffice for each bounded series operand; temporary
    // products/carries fit UInt128. Unresolved rounding keeps the existing fallback.
    internal static bool TryFixed(ExactMeanAccumulator.ProductFactor current,
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
        var core = FixedSeries(negative ? d - n : n - d, n + d);
        Int128 lower = negative ? -(Int128)core.High : (Int128)core.Low;
        Int128 upper = negative ? -(Int128)core.Low : (Int128)core.High;
        lower += exponent * (Int128)(exponent < 0 ? FixedLogTwo.High : FixedLogTwo.Low);
        upper += exponent * (Int128)(exponent < 0 ? FixedLogTwo.Low : FixedLogTwo.High);
        var a = FixedToDouble(lower);
        var b = FixedToDouble(upper);
        if (a != b) return false; // NOSONAR: certification requires equal rounded endpoints.
        value = a;
        return true;
    }

    private static (UInt128 Low, UInt128 High) FixedSeries(ulong numerator, ulong denominator)
    {
        if (numerator == 0) return (0, 0);
        // Divide in two stages so a 55-bit numerator never needs a 151-bit shift.
        UInt128 scaled = (UInt128)numerator << 64;
        var (first, remainder) = ExactMeanAccumulator.CompactDivRem(scaled, denominator);
        scaled = remainder << 32;
        var (second, tail) = ExactMeanAccumulator.CompactDivRem(scaled, denominator);
        UInt128 low = (first << 32) + second;
        UInt128 high = low + (tail == 0 ? (UInt128)0 : 1);
        UInt128 squareLow = FixedProduct(low, low, false);
        UInt128 squareHigh = FixedProduct(high, high, true);
        UInt128 powerLow = low, powerHigh = high, sumLow = 0, sumHigh = 0;
        for (uint odd = 1; ; odd += 2)
        {
            sumLow += powerLow / odd;
            sumHigh += (powerHigh + odd - 1) / odd;
            powerLow = FixedProduct(powerLow, squareLow, false);
            powerHigh = FixedProduct(powerHigh, squareHigh, true);
            // |atanh argument| <= 1/3, including log(2). Once powerHigh <= 1,
            // the remaining geometric tail is less than one fixed-point unit:
            // 1 / ((odd + 2) * (1 - squareHigh / 2^96)) < 1.
            if (powerHigh <= 1) return (2 * sumLow, 2 * (sumHigh + 1));
        }
    }

    private static UInt128 FixedProduct(UInt128 left, UInt128 right, bool ceiling)
    {
        UInt128 leftLow = left & LimbMask, leftHigh = left >> 48;
        UInt128 rightLow = right & LimbMask, rightHigh = right >> 48;
        UInt128 low = leftLow * rightLow;
        UInt128 cross = leftLow * rightHigh + leftHigh * rightLow;
        UInt128 carry = (low >> 48) + (cross & LimbMask);
        UInt128 result = leftHigh * rightHigh + (cross >> 48) + (carry >> 48);
        bool remainder = (low & LimbMask) != 0 || (carry & LimbMask) != 0;
        return result + (ceiling && remainder ? (UInt128)1 : 0);
    }

    private static double FixedToDouble(Int128 value)
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
        double result = Math.ScaleB((double)rounded, shift - 96);
        return negative ? -result : result;
    }
}
#endif

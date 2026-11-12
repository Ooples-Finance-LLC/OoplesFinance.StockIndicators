#if !NETFRAMEWORK
using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal sealed partial class HilbertPhaseState
{
    // The binary64 weights share an exact integer coefficient:
    // .2*a + .8*b = 3602879701896397 * (a + 4*b) * 2^-54.
    // Keep the sum and product exact, then round once. Unsupported ranges
    // retain the existing floating certificate and extended dyadic fallback.
    internal static bool TryWordBlend(double value, double old, out double rounded)
    {
        rounded = 0;
        if (!double.IsFinite(value) || !double.IsFinite(old)) return false;
        var a = new ExactMeanAccumulator.ProductFactor(value);
        var b = new ExactMeanAccumulator.ProductFactor(old, 2);
        if (a.Mantissa == 0 && b.Mantissa == 0) return true;
        int aScale = a.Mantissa == 0 ? b.Scale : a.Scale;
        int bScale = b.Mantissa == 0 ? aScale : b.Scale;
        int scale = Math.Min(aScale, bScale);
        int aShift = aScale - scale, bShift = bScale - scale;
        ulong aMagnitude = (ulong)Math.Abs(a.Mantissa), bMagnitude = (ulong)Math.Abs(b.Mantissa);
        if (64 - BitOperations.LeadingZeroCount(aMagnitude) + aShift > 75
            || 64 - BitOperations.LeadingZeroCount(bMagnitude) + bShift > 75) return false;

        Int128 sum = ((Int128)a.Mantissa << aShift) + ((Int128)b.Mantissa << bShift);
        if (sum == 0) return true;
        bool negative = sum < 0;
        UInt128 magnitude = (UInt128)(negative ? -sum : sum);
        // Each aligned operand has at most 75 bits, so the sum is <2^76.
        // The coefficient is <2^52: the exact product fits in UInt128.
        UInt128 product = unchecked(magnitude * 3602879701896397UL);
        ulong high = (ulong)(product >> 64);
        int width = high == 0 ? 64 - BitOperations.LeadingZeroCount((ulong)product)
            : 128 - BitOperations.LeadingZeroCount(high);
        // ProductFactor scales are in units of 2^-1074, and the weight
        // contributes another 2^-54. Only normal finite results are certified.
        int exponent = width - 1 + scale - 1128;
        if (exponent < -1022 || exponent > 1023) return false;
        int shift = width - 53;
        UInt128 significand;
        if (shift > 0)
        {
            significand = product >> shift;
            UInt128 remainder = product - (significand << shift);
            UInt128 half = (UInt128)1 << (shift - 1);
            if (remainder > half || (remainder == half && (significand & 1) != 0)) significand++;
        }
        else significand = product << -shift;
        if (significand == (UInt128)1 << 53)
        {
            significand >>= 1;
            if (++exponent > 1023) return false;
        }
        ulong bits = ((ulong)(exponent + 1023) << 52) | ((ulong)significand & ((1UL << 52) - 1));
        if (negative) bits |= 1UL << 63;
        rounded = BitConverter.Int64BitsToDouble(unchecked((long)bits));
        return true;
    }
}
#endif

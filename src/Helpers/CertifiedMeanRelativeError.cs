#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Helpers;

// Cache exact reciprocal bounds once per retained input. Window sums and
// fixed-width products enclose the signed error; accept only when both
// endpoints round to the same double.
// Wide exponent spreads, subnormals, zeros, and uncertain rounding retain the
// existing rational implementation; the stored history is never approximated.
internal static class CertifiedMeanRelativeError
{
    private const int Precision = 66;

    internal static void Reciprocal(double value, out UInt128 floor, out bool exact, out long significand, out int exponent)
    {
        floor = 0; exact = false;
        if (!Parts(value, out significand, out exponent)) return;
        var divided = UInt128.DivRem((UInt128)1 << 127, (UInt128)Math.Abs(significand));
        floor = divided.Quotient; exact = divided.Remainder == 0;
    }

    internal static bool TryCompute(Queue<RoundedMeanErrorWindow.Sample> values, double mean, out double result)
    {
        result = 0;
        if (values.Count == 0) return false;
        bool zeroMean = mean == 0;
        if (!Parts(mean, out long center, out int centerExponent) && !zeroMean) return false;
        Int128 lower = 0, upper = 0;
        int balance = 0;
        foreach (var sample in values)
        {
            long price = sample.NormalSignificand; int exponent = sample.NormalExponent;
            if (exponent is <= 0 or >= 2047) return false;
            if (zeroMean)
            {
                balance += price > 0 ? 1 : -1;
                continue;
            }
            if (sample.Value == mean) continue; // Equal binary64 inputs have an exactly zero error.
            if (Math.Abs(exponent - centerExponent) > 3) return false;
            // All reciprocals share scale 2^(-127 - (meanExponent + 3 - 1075)).
            // The shift is 0..6; each bound has at most 82 bits, and even an
            // Int32-sized signed sum fits in Int128.
            int shift = centerExponent + 3 - exponent;
            Int128 reciprocalLow = (Int128)(sample.ReciprocalFloor << shift);
            Int128 reciprocalHigh = reciprocalLow + (sample.ReciprocalExact ? 0 : (Int128)1 << shift);
            if (price < 0) (reciprocalLow, reciprocalHigh) = (-reciprocalHigh, -reciprocalLow);
            // |price - mean| / price is mean/price - 1 below the mean,
            // and 1 - mean/price above it, including negative prices.
            // Each signed bound is smaller than 2^82 in magnitude. A Queue
            // holds at most Int32.MaxValue samples, so every partial sum is
            // smaller than 2^113: these additions cannot overflow Int128.
            if (sample.Value < mean)
            {
                lower = unchecked(lower + reciprocalLow);
                upper = unchecked(upper + reciprocalHigh); balance--;
            }
            else
            {
                lower = unchecked(lower - reciprocalHigh);
                upper = unchecked(upper - reciprocalLow); balance++;
            }
        }
        if (zeroMean) return Publish((Int128)balance << Precision, values.Count, out result);
        if (center < 0) (lower, upper) = (upper, lower);
        ProductBounds(center, lower, out var floor, out _);
        ProductBounds(center, upper, out _, out var ceiling);
        // Multiplication by the mean cancels its binary exponent, leaving a
        // 130-bit fractional grid. Discard 64 bits outwards to fit publication
        // bounds in Int128, then add the exact signed count on the 66-bit grid.
        lower = ((Int128)balance << Precision) + floor;
        upper = ((Int128)balance << Precision) + ceiling;
        if (!Publish(lower, values.Count, out double low) || !Publish(upper, values.Count, out double high)) return false;
        if (BitConverter.DoubleToInt64Bits(low) != BitConverter.DoubleToInt64Bits(high)) return false;
        result = low;
        return true;
    }

    private static void ProductBounds(long coefficient, Int128 value, out Int128 floor, out Int128 ceiling)
    {
        bool negative = (coefficient < 0) != (value < 0);
        UInt128 magnitude = (UInt128)(value < 0 ? -value : value);
        ulong factor = (ulong)Math.Abs(coefficient);
        // Exact 192-bit product represented by its two partial products.
        // Only the low 64 bits are dropped, with directed rounding. The upper
        // product is below 2^103 for every accepted Int32-sized window.
        UInt128 low = (UInt128)unchecked((ulong)magnitude) * factor;
        UInt128 high = (magnitude >> 64) * factor + (low >> 64);
        bool inexact = unchecked((ulong)low) != 0;
        if (negative) { ceiling = -(Int128)high; floor = ceiling - (inexact ? 1 : 0); }
        else { floor = (Int128)high; ceiling = floor + (inexact ? 1 : 0); }
    }

    // Align ordinary prices and the published mean on one exact binary grid.
    // Absolute sums need at most 91 bits even for Int32.MaxValue samples.
    // Squared sums check every addition; overflow retains the rational path.
    internal static bool TryError(Queue<RoundedMeanErrorWindow.Sample> values, double mean, bool squared, out double result)
    {
        result = 0;
        if (values.Count == 0 || !Parts(mean, out long center, out int centerExponent)) return false;
        int common = centerExponent - 3;
        long alignedCenter = center << 3;
        UInt128 sum = 0;
        foreach (var sample in values)
        {
            long price = sample.NormalSignificand; int exponent = sample.NormalExponent;
            if (exponent is <= 0 or >= 2047 || Math.Abs(exponent - centerExponent) > 3) return false;
            // A 53-bit significand shifted by at most six bits and the
            // 56-bit center have a difference strictly inside Int64.
            long difference = (price << (exponent - common)) - alignedCenter;
            UInt128 term = (UInt128)Math.Abs(difference);
            if (squared) term *= term;
            if (term > UInt128.MaxValue - sum) return false;
            sum += term;
        }
        if (sum == 0) return true;
        int scale = common - 1075;
        // Transfer a fixed binary factor to the denominator so the ratio's
        // unscaled exponent remains inside the midpoint certifier's bounds.
        return CertifiedCompactRatio.TryDivide(sum, (UInt128)values.Count << 64,
            (squared ? 2 * scale : scale) + 64, out result);
    }

    // Full diagnostics need both moments of the same deviations. Decode and
    // align each retained sample once, keeping both sums exact in fixed width.
    internal static bool TryErrors(Queue<RoundedMeanErrorWindow.Sample> values, double mean,
        out double absolute, out double squared)
    {
        absolute = squared = 0;
        if (values.Count == 0 || !Parts(mean, out long center, out int centerExponent)) return false;
        int common = centerExponent - 3;
        long alignedCenter = center << 3;
        UInt128 absoluteSum = 0, squaredSum = 0;
        foreach (var sample in values)
        {
            int exponent = sample.NormalExponent;
            if (exponent is <= 0 or >= 2047 || Math.Abs(exponent - centerExponent) > 3) return false;
            long difference = (sample.NormalSignificand << (exponent - common)) - alignedCenter;
            UInt128 distance = (UInt128)Math.Abs(difference);
            UInt128 square = distance * distance;
            if (square > UInt128.MaxValue - squaredSum) return false;
            // At most Int32.MaxValue terms, each strictly below 2^60.
            absoluteSum += distance;
            squaredSum += square;
        }
        if (absoluteSum == 0) return true;
        int scale = common - 1075;
        UInt128 denominator = (UInt128)values.Count << 64;
        return CertifiedCompactRatio.TryDivide(absoluteSum, denominator, scale + 64, out absolute)
            && CertifiedCompactRatio.TryDivide(squaredSum, denominator, 2 * scale + 64, out squared);
    }

    private static bool Parts(double value, out long significand, out int exponent)
    {
        ulong bits = unchecked((ulong)BitConverter.DoubleToInt64Bits(value));
        exponent = (int)((bits >> 52) & 0x7ff);
        significand = (long)((bits & 0xfffffffffffffUL) | (1UL << 52));
        if ((bits >> 63) != 0) significand = -significand;
        return exponent is > 0 and < 2047;
    }

    private static bool Publish(Int128 value, int count, out double result)
    {
        result = 0;
        if (value == 0) return true;
        UInt128 magnitude = (UInt128)(value < 0 ? -value : value);
        if (!CertifiedCompactRatio.TryDivide(magnitude, (UInt128)count << Precision, 0, out result)) return false;
        if (value < 0) result = -result;
        return true;
    }
}
#endif

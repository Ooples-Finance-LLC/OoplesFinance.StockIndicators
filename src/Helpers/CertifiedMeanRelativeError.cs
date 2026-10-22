#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Helpers;

// Each signed quotient is enclosed on a 72-bit fractional grid using exact
// integer division. Accept only when both endpoints round to the same double.
// Wide exponent spreads, subnormals, zeros, and uncertain rounding retain the
// existing rational implementation; the stored history is never approximated.
internal static class CertifiedMeanRelativeError
{
    private const int Precision = 72;

    internal static bool TryCompute(Queue<RoundedMeanErrorWindow.Sample> values, double mean, out double result)
    {
        result = 0;
        if (values.Count == 0) return false;
        bool zeroMean = mean == 0;
        if (!Parts(mean, out long center, out int centerExponent) && !zeroMean) return false;
        Int128 lower = 0, upper = 0;
        foreach (var sample in values)
        {
            if (!Parts(sample.Value, out long price, out int exponent)) return false;
            UInt128 quotient;
            bool inexact;
            if (zeroMean)
            {
                quotient = (UInt128)1 << Precision;
                inexact = false;
            }
            else
            {
                if (Math.Abs(exponent - centerExponent) > 3) return false;
                int common = Math.Min(exponent, centerExponent);
                long denominator = price << (exponent - common);
                long difference = denominator - (center << (centerExponent - common));
                // Aligned significands have at most 56 bits; their signed
                // difference fits Int64. Check the subsequent UInt128 shift.
                UInt128 magnitude = (UInt128)Math.Abs(difference);
                if (magnitude > UInt128.MaxValue >> Precision) return false;
                var divided = UInt128.DivRem(magnitude << Precision, (UInt128)Math.Abs(denominator));
                quotient = divided.Quotient;
                inexact = divided.Remainder != 0;
            }
            // The exponent-spread guard bounds each quotient by 2^77. Even
            // Int32.MaxValue samples therefore fit comfortably in Int128.
            Int128 floor = (Int128)quotient;
            Int128 ceiling = floor + (inexact ? 1 : 0);
            if (price > 0) { lower += floor; upper += ceiling; }
            else { lower -= ceiling; upper -= floor; }
        }
        if (!Publish(lower, values.Count, out double low) || !Publish(upper, values.Count, out double high)) return false;
        if (BitConverter.DoubleToInt64Bits(low) != BitConverter.DoubleToInt64Bits(high)) return false;
        result = low;
        return true;
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
            if (!Parts(sample.Value, out long price, out int exponent) || Math.Abs(exponent - centerExponent) > 3) return false;
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

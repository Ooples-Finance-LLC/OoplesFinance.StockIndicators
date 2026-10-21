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

#if !NETFRAMEWORK
using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// The stage recurrence is an exact fixed-width ratio. The final polynomial
// uses outward-rounded integer bounds; only identical binary64 endpoints
// certify publication. Unsupported ranges retain the original exact path.
internal static class CertifiedTillson
{
    private static bool Parts(double value, out long mantissa, out int exponent)
    {
        long bits = BitConverter.DoubleToInt64Bits(value);
        int raw = (int)((bits >> 52) & 2047);
        mantissa = (bits & 0xfffffffffffffL) | (1L << 52);
        if (bits < 0) mantissa = -mantissa;
        exponent = raw - 1075;
        if (value == 0) { mantissa = 0; return true; }
        return raw is > 0 and < 2047;
    }

    internal static bool TrySmooth(double prior, double input, int period, out double result)
    {
        result = 0;
        if (!Parts(prior, out long a, out int ae) || !Parts(input, out long b, out int be)) return false;
        if (a == 0) ae = be;
        if (b == 0) be = ae;
        if (Math.Abs(ae - be) > 12) return false;
        int scale = Math.Min(ae, be);
        // At most 65 input bits and 31 weight bits: every signed sum fits.
        Int128 numerator = ((Int128)a << (ae - scale)) * (period - 1L)
            + ((Int128)b << (be - scale + 1));
        if (numerator == 0) return true;
        bool negative = numerator < 0;
        UInt128 magnitude = (UInt128)(negative ? -numerator : numerator);
        if (magnitude <= ulong.MaxValue && TrySmooth64((ulong)magnitude, (ulong)period + 1, scale, out result))
        {
            if (negative) result = -result;
            return true;
        }
        if (!CertifiedCompactRatio.TryDivide(magnitude, (UInt128)((long)period + 1) << 64,
                scale + 64, out result)) return false;
        if (negative) result = -result;
        return true;
    }

    // Use one exact machine-word division when the rounding-grid operands fit.
    // Wider operands and subnormal/overflow boundaries retain the existing path.
    private static bool TrySmooth64(ulong top, ulong bottom, int scale, out double result)
    {
        result = 0;
        int topBits = 64 - BitOperations.LeadingZeroCount(top);
        int bottomBits = 64 - BitOperations.LeadingZeroCount(bottom);
        int power = topBits - bottomBits;
        if (power >= 0 ? top < (bottom << power) : (top << -power) < bottom) power--;
        if ((long)power + scale is < -1022 or > 1023) return false;
        int shift = 52 - power;
        if (shift >= 0)
        {
            if (shift >= 64 || topBits + shift > 64) return false;
            top <<= shift;
        }
        else
        {
            if (-shift >= 64 || bottomBits - shift > 64) return false;
            bottom <<= -shift;
        }
        (ulong quotient, ulong remainder) = Math.DivRem(top, bottom);
        ulong complement = bottom - remainder;
        if (remainder > complement || remainder == complement && (quotient & 1) != 0) quotient++;
        // The rounded significand has at most 53 bits (or is exactly 2^53).
        // Scaling a normal result is exact, so there is no second rounding.
        result = Math.ScaleB((double)quotient, scale - shift);
        return double.IsFinite(result);
    }

    internal static bool TryCombine(double third, double fourth, double fifth, double sixth,
        double factor, out double result)
    {
        result = 0;
        // Restrict the factor so all three signed 53-bit products fit Int128.
        if (Math.Abs(factor) is < .25 or > 2 || !double.IsFinite(factor)) return false;
        if (!Parts(third, out long a, out int ae) || !Parts(fourth, out long b, out int be)
            || !Parts(fifth, out long c, out int ce) || !Parts(sixth, out long d, out int de)) return false;
        int maximum = Math.Max(Math.Max(a == 0 ? int.MinValue : ae, b == 0 ? int.MinValue : be),
            Math.Max(c == 0 ? int.MinValue : ce, d == 0 ? int.MinValue : de));
        if (maximum == int.MinValue) return true;
        int scale = maximum - 12;
        if (!Align(a, ae, scale, out Int128 av) || !Align(b, be, scale, out Int128 bv)
            || !Align(c, ce, scale, out Int128 cv) || !Align(d, de, scale, out Int128 dv)) return false;
        Parts(factor, out long weight, out int power);
        Int128 first = 3 * (av - bv), second = 3 * (av - 2 * bv + cv);
        Int128 low = av - 3 * bv + 3 * cv - dv, high = low;
        Multiply(ref low, ref high, weight, -power);
        low += second; high += second;
        Multiply(ref low, ref high, weight, -power);
        low += first; high += first;
        Multiply(ref low, ref high, weight, -power);
        low += av; high += av;
        double lower = JurikDyadic.FromBinaryProduct(low, scale).Mean(1);
        double upper = JurikDyadic.FromBinaryProduct(high, scale).Mean(1);
        if (!double.IsFinite(lower) || BitConverter.DoubleToInt64Bits(lower) != BitConverter.DoubleToInt64Bits(upper)) return false;
        result = lower;
        return true;
    }

    private static bool Align(long value, int exponent, int scale, out Int128 aligned)
    {
        aligned = 0;
        if (value == 0) return true;
        int shift = exponent - scale;
        if (shift is < 0 or > 12) return false;
        aligned = (Int128)value << shift;
        return true;
    }

    private static void Multiply(ref Int128 low, ref Int128 high, long weight, int shift)
    {
        // Each stage input has magnitude <2^65. For |factor|<=2, the
        // largest pre-product magnitude is <62*2^65 plus rounding units;
        // multiplying by a 53-bit significand stays below 2^125.
        if (weight < 0) (low, high) = (high, low);
        Int128 lowerProduct = low * weight, upperProduct = high * weight;
        low = lowerProduct >> shift;
        high = upperProduct >> shift;
        if ((upperProduct & (((Int128)1 << shift) - 1)) != 0) high++;
    }
}
#endif

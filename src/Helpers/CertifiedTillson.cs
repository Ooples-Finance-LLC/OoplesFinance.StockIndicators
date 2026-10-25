#if !NETFRAMEWORK
using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// The stage recurrence is an exact fixed-width ratio. The final polynomial
// uses outward-rounded integer bounds; only identical binary64 endpoints
// certify publication. Unsupported ranges retain the original exact path.
internal static class CertifiedTillson
{
    // Same-binade positive normals admit a 64-bit exact numerator. The cached
    // reciprocal estimates its quotient at most one low; correction and ties
    // to even therefore recover the exact binary64 convex mean.
    internal readonly struct WordSmoother
    {
        private readonly ulong _denominator, _reciprocal;

        internal bool IsSupported => _denominator != 0;

        internal WordSmoother(int period)
        {
            _denominator = 0;
            _reciprocal = 0;
            if (period is < 1 or > 2047) return;
            _denominator = (ulong)period + 1;
            _reciprocal = (ulong)(((UInt128)1 << 64) / _denominator);
        }

        internal bool TrySmooth(double prior, double input, out double result)
        {
            result = 0;
            if (_denominator == 0) return false;
            ulong priorBits = unchecked((ulong)BitConverter.DoubleToInt64Bits(prior));
            ulong inputBits = unchecked((ulong)BitConverter.DoubleToInt64Bits(input));
            ulong exponent = priorBits >> 52;
            if (exponent is < 1 or > 2046 || inputBits >> 52 != exponent) return false;
            result = SmoothSameBinade(priorBits, inputBits, exponent);
            return true;
        }

        // Caller establishes a supported period and positive normal operands
        // with this same exponent. Convex means preserve the binade.
        internal double SmoothSameBinade(ulong priorBits, ulong inputBits, ulong exponent)
        {
            // n <= 2^64-2048, q*d <= n, and the corrected remainder < d.
            // The rounded significand remains below 2^53. These proven bounds
            // make arithmetic overflow checks redundant inside this kernel.
            unchecked
            {
                const ulong fractionMask = (1UL << 52) - 1;
                ulong a = (priorBits & fractionMask) | (1UL << 52);
                ulong b = (inputBits & fractionMask) | (1UL << 52);
                // d <= 2048 bounds n by 2^64 - 2048, including period 2047.
                ulong numerator = a * (_denominator - 2) + 2 * b;
                ulong quotient = Math.BigMul(numerator, _reciprocal, out _);
                ulong remainder = numerator - quotient * _denominator;
                if (remainder >= _denominator) { quotient++; remainder -= _denominator; }
                ulong complement = _denominator - remainder;
                if (remainder > complement || remainder == complement && (quotient & 1) != 0) quotient++;
                // Convex same-binade inputs keep the rounded significand in range.
                return BitConverter.Int64BitsToDouble((long)((exponent << 52) | (quotient & fractionMask)));
            }
        }
    }


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

    // Exact product and TwoSum residuals are normal within this bounded range.
    // Every subsequent addition/FMA is enclosed by adjacent representable values.
    // Strict containment inside the quotient rounding cell excludes midpoint ties.
    internal static bool TrySmoothFloating(double prior, double input, int period, out double result)
    {
        result = 0;
        const double minimum = 3.4395525670743494E-136; // 2^-450
        const double maximum = 2.9073548971824276E+135; // 2^450
        double a = Math.Abs(prior), b = Math.Abs(input);
        if (period < 1 || !double.IsFinite(a) || !double.IsFinite(b)
            || a > maximum || b > maximum
            || a != 0 && a < minimum || b != 0 && b < minimum) return false;
        double weight = period - 1d, denominator = period + 1d;
        double product = prior * weight, twice = input * 2;
        double productError = Math.FusedMultiplyAdd(prior, weight, -product);
        double sum = product + twice;
        double virtualTwice = sum - product;
        double sumError = (product - (sum - virtualTwice)) + (twice - virtualTwice);
        double quotient = sum / denominator;
        if (Math.Abs(quotient) < minimum || !double.IsFinite(quotient)) return false;
        double tail = productError + sumError;
        double residual = Math.FusedMultiplyAdd(-quotient, denominator, sum);
        double lower = Math.BitDecrement(Math.BitDecrement(residual) + Math.BitDecrement(tail));
        double upper = Math.BitIncrement(Math.BitIncrement(residual) + Math.BitIncrement(tail));
        double below = (quotient - Math.BitDecrement(quotient)) * .5;
        double above = (Math.BitIncrement(quotient) - quotient) * .5;
        double lowerBoundary = Math.BitIncrement(-below * denominator);
        double upperBoundary = Math.BitDecrement(above * denominator);
        if (!(lower > lowerBoundary && upper < upperBoundary)) return false;
        result = quotient;
        return true;
    }

    internal static bool TrySmooth(double prior, double input, int period, out double result)
    {
        if (TrySmoothFloating(prior, input, period, out result)) return true;
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
        // Alignment and factor guards bound every product below 2^125.
        unchecked
        {
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
        unchecked
        {
            if (weight < 0) (low, high) = (high, low);
            Int128 lowerProduct = low * weight, upperProduct = high * weight;
            low = lowerProduct >> shift;
            high = upperProduct >> shift;
            if ((upperProduct & (((Int128)1 << shift) - 1)) != 0) high++;
        }
    }
}
#endif

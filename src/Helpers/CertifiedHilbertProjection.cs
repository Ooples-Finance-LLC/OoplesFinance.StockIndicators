#if !NETFRAMEWORK
using System.Runtime.CompilerServices;

namespace OoplesFinance.StockIndicators.Helpers;

// Floating residual bounds, then outward integer bounds, for complete projections. A result
// is accepted only when each pair of endpoints rounds to one finite binary64
// value. Tiny near-axis terms are enclosed, never discarded. Ambiguous phase
// decisions and extended exponents retain the original exact projection.
internal static class CertifiedHilbertProjection
{
    private readonly record struct Factor(long Mantissa, int Shift);
    private readonly record struct Coefficient(Factor Sine, Factor Cosine);
    private static readonly Coefficient[][] Coefficients = CreateCoefficients();
    private static readonly (double Sine, double Cosine)[][] FloatingCoefficients = CreateFloatingCoefficients();
    private const double MinimumFloatingMagnitude = 3.4395525670743494E-136; // 2^-450
    private static readonly bool[] FloatingCoefficientCertificates = CreateFloatingCoefficientCertificates();

    private static bool[] CreateFloatingCoefficientCertificates()
    {
        var result = new bool[FloatingCoefficients.Length];
        for (int count = 0; count < result.Length; count++)
        {
            result[count] = true;
            foreach (var coefficient in FloatingCoefficients[count])
            {
                if (Math.Abs(coefficient.Sine) is > 0 and < MinimumFloatingMagnitude
                    || Math.Abs(coefficient.Cosine) is > 0 and < MinimumFloatingMagnitude)
                {
                    result[count] = false;
                    break;
                }
            }
        }
        return result;
    }

    private static (double Sine, double Cosine)[][] CreateFloatingCoefficients()
    {
        var result = new (double Sine, double Cosine)[51][];
        for (int count = 0; count <= 50; count++)
        {
            result[count] = new (double Sine, double Cosine)[count];
            for (int lag = 0; lag < count; lag++)
            {
                double angle = lag * 2d * Math.PI / count;
                result[count][lag] = (Math.Sin(angle), Math.Cos(angle));
            }
        }
        return result;
    }


    private static Coefficient[][] CreateCoefficients()
    {
        var result = new Coefficient[51][];
        for (int count = 0; count <= 50; count++)
        {
            result[count] = new Coefficient[count];
            for (int lag = 0; lag < count; lag++)
            {
                // Match the original angle's binary64 operation order exactly.
                double angle = lag * 2d * Math.PI / count;
                result[count][lag] = new(Of(Math.Sin(angle)), Of(Math.Cos(angle)));
            }
        }
        return result;
    }

    private static Factor Of(double value)
    {
        if (value == 0) return default;
        long bits = BitConverter.DoubleToInt64Bits(value);
        int exponent = (int)((bits >> 52) & 2047);
        long mantissa = (bits & 0xfffffffffffffL) | (exponent == 0 ? 0 : 1L << 52);
        if (bits < 0) mantissa = -mantissa;
        return new(mantissa, 1075 - Math.Max(1, exponent));
    }

    internal static bool TryProject(JurikDyadic[] values, int cursor, int count,
        out JurikDyadic real, out JurikDyadic imaginary)
    {
        real = imaginary = default;
        if (values.Length != 50 || cursor is < 0 or >= 50 || count is < 0 or > 50) return false;
        if (count == 0) return true;
        if (TryProjectFloating(values, cursor, count, out real, out imaginary)) return true;
        if (!values[cursor].TryDouble(out double anchor)) return false;
        int anchorExponent = (int)((BitConverter.DoubleToInt64Bits(anchor) >> 52) & 2047);
        if (anchorExponent is 0 or 2047) return false;
        int grid = anchorExponent - 18;
        Int128 realLow = 0, realHigh = 0, imaginaryLow = 0, imaginaryHigh = 0;
        int index = cursor;
        var coefficients = Coefficients[count];
        for (int lag = 0; lag < count; lag++)
        {
            if (!values[index].TryDouble(out double value)) return false;
            if (value != 0)
            {
                long bits = BitConverter.DoubleToInt64Bits(value);
                int exponent = (int)((bits >> 52) & 2047);
                int shift = exponent - grid;
                if (exponent is 0 or 2047 || shift is < 0 or > 20) return false;
                Int128 integer = (Int128)((bits & 0xfffffffffffffL) | (1L << 52)) << shift;
                if (bits < 0) integer = -integer;
                // Inputs have fewer than 73 magnitude bits, coefficients at
                // most 53. Every exact product is strictly below 2^126.
                var coefficient = coefficients[lag];
                AddProduct(integer, coefficient.Sine, ref realLow, ref realHigh);
                AddProduct(integer, coefficient.Cosine, ref imaginaryLow, ref imaginaryHigh);
            }
            if (--index < 0) index = 49;
        }
        // Coefficients have magnitude at most one. Fifty outward-rounded
        // terms need fewer than 80 magnitude bits in each accumulated bound.
        int scale = grid - 1075;
        if (!Publish(realLow, realHigh, scale, out double realValue)
            || !Publish(imaginaryLow, imaginaryHigh, scale, out double imaginaryValue)) return false;
        real = JurikDyadic.FromDouble(realValue);
        imaginary = JurikDyadic.FromDouble(imaginaryValue);
        return true;
    }

#pragma warning disable S1244 // Only exact zero terms may be skipped; near-axis nonzero terms retain their residuals.
    // FMA retains each exact product residual; TwoSum retains every exact
    // addition residual. A normal-grid summation bound encloses their accumulated
    // rounding error. Only endpoints rounding to identical doubles certify the
    // complete projection.
    internal static bool TryProjectFloating(JurikDyadic[] values, int cursor, int count,
        out JurikDyadic real, out JurikDyadic imaginary)
    {
        real = imaginary = default;
        if (values.Length != 50 || cursor is < 0 or >= 50 || count is < 0 or > 50) return false;
        // Coefficients never change; certify their product-residual range once
        // per cached window instead of repeating the same check for every price.
        if (!FloatingCoefficientCertificates[count]) return false;
        const long minimumBits = 0x23d0000000000000L; // 2^-450
        const long maximumBits = 0x5c10000000000000L; // 2^450
        double realSum = 0, realResidual = 0, realMaximum = 0;
        double imaginarySum = 0, imaginaryResidual = 0, imaginaryMaximum = 0;
        var coefficients = FloatingCoefficients[count];
        int index = cursor;
        for (int lag = 0; lag < count; lag++)
        {
            if (!values[index].TryDouble(out double value)) return false;
            // Positive binary64 encodings follow magnitude order. The exact
            // inclusive range also rejects infinities/NaNs; either zero is valid.
            long magnitudeBits = BitConverter.DoubleToInt64Bits(value) & long.MaxValue;
            if (magnitudeBits != 0 && (magnitudeBits < minimumBits || magnitudeBits > maximumBits)) return false;
            var coefficient = coefficients[lag];
            AccumulateFloating(value, coefficient.Sine, ref realSum, ref realResidual, ref realMaximum);
            AccumulateFloating(value, coefficient.Cosine, ref imaginarySum, ref imaginaryResidual, ref imaginaryMaximum);
            if (--index < 0) index = 49;
        }
        if (!PublishFloating(realSum, realResidual, realMaximum, out double realValue)
            || !PublishFloating(imaginarySum, imaginaryResidual, imaginaryMaximum, out double imaginaryValue)) return false;
        real = JurikDyadic.FromDouble(realValue);
        imaginary = JurikDyadic.FromDouble(imaginaryValue);
        return true;
    }

    // Bounded products, fifty-term sums and their residual additions stay
    // finite. These private steps preserve both zero transitions and gradual
    // underflow while avoiding non-finite handling at each error addition.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double DecrementFinite(double value)
    {
        long bits = BitConverter.DoubleToInt64Bits(value);
        if (bits == 0) return -double.Epsilon;
        return BitConverter.Int64BitsToDouble(bits + (bits < 0 ? 1 : -1));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double IncrementFinite(double value)
    {
        long bits = BitConverter.DoubleToInt64Bits(value);
        if (bits == long.MinValue) return double.Epsilon;
        return BitConverter.Int64BitsToDouble(bits + (bits < 0 ? -1 : 1));
    }

    private static bool PublishFloating(double sum, double residual, double maximum, out double value)
    {
        // Zero maximum means every captured residual was zero: the leading
        // sum is exact. The caller's FromDouble publication canonicalizes zero.
        if (maximum == 0)
        {
            value = sum;
            return double.IsFinite(value);
        }
        // Each component has at most 100 rounded residual additions. Products
        // and TwoSum residuals lie on the 2^-1006 grid, so nonzero partial sums
        // remain normal. With u = 2^-53, 100*gamma(100) < 2^-39. The maximum
        // bounds each residual, making 2^-38 * maximum a conservative enclosure
        // of all residual-sum rounding. Round its scaled value outward too.
        double bound = IncrementFinite(maximum * 3.637978807091713E-12); // 2^-38
        double lower = sum + DecrementFinite(residual - bound);
        double upper = sum + IncrementFinite(residual + bound);
        value = lower;
        return double.IsFinite(lower)
            && BitConverter.DoubleToInt64Bits(lower) == BitConverter.DoubleToInt64Bits(upper);
    }

    private static void AccumulateFloating(double value, double coefficient,
        ref double sum, ref double residual, ref double maximum)
    {
        if (value == 0 || coefficient == 0) return;
        // Nonzero products are >=2^-900; their exact residuals are multiples
        // of 2^-1006 and remain normal. At most fifty |coefficient|<=1 terms
        // keep the leading sum below 2^456, excluding TwoSum overflow.
        double product = value * coefficient;
        double productError = Math.FusedMultiplyAdd(value, coefficient, -product);
        double next = sum + product;
        double virtualProduct = next - sum;
        double sumError = (sum - (next - virtualProduct)) + (product - virtualProduct);
        residual = (residual + productError) + sumError;
        // A rounded sum of nonnegative representable magnitudes is at least
        // either operand, so this maximum bounds every captured residual.
        maximum = Math.Max(maximum, Math.Abs(productError) + Math.Abs(sumError));
        sum = next;
    }
#pragma warning restore S1244

    private static void AddProduct(Int128 value, Factor factor, ref Int128 lower, ref Int128 upper)
    {
        if (factor.Mantissa == 0) return;
        Int128 product = unchecked(value * factor.Mantissa);
        if (factor.Shift >= 127)
        {
            // Any such nonzero product lies strictly between -1 and 1 on
            // the common grid; retain its directed interval and its sign.
            if (product < 0) lower--;
            else if (product > 0) upper++;
            return;
        }
        Int128 floor = product >> factor.Shift;
        lower = unchecked(lower + floor);
        upper = unchecked(upper + floor);
        if ((product & (((Int128)1 << factor.Shift) - 1)) != 0) upper++;
    }

    private static bool Publish(Int128 lower, Int128 upper, int scale, out double value)
    {
        value = JurikDyadic.FromBinaryProduct(lower, scale).Mean(1);
        double ceiling = JurikDyadic.FromBinaryProduct(upper, scale).Mean(1);
        return double.IsFinite(value)
            && BitConverter.DoubleToInt64Bits(value) == BitConverter.DoubleToInt64Bits(ceiling);
    }
}
#endif

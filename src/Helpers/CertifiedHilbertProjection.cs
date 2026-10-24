#if !NETFRAMEWORK
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
    // addition residual. Outward additions enclose the residual sum. Only
    // endpoints rounding to identical doubles certify the complete projection.
    internal static bool TryProjectFloating(JurikDyadic[] values, int cursor, int count,
        out JurikDyadic real, out JurikDyadic imaginary)
    {
        real = imaginary = default;
        if (values.Length != 50 || cursor is < 0 or >= 50 || count is < 0 or > 50) return false;
        const double minimum = 3.4395525670743494E-136; // 2^-450
        const double maximum = 2.9073548971824276E+135; // 2^450
        double realSum = 0, realLow = 0, realHigh = 0;
        double imaginarySum = 0, imaginaryLow = 0, imaginaryHigh = 0;
        var coefficients = FloatingCoefficients[count];
        int index = cursor;
        for (int lag = 0; lag < count; lag++)
        {
            if (!values[index].TryDouble(out double value)) return false;
            double magnitude = Math.Abs(value);
            if (!double.IsFinite(value) || magnitude > maximum || magnitude != 0 && magnitude < minimum) return false;
            var coefficient = coefficients[lag];
            // Checking the cached coefficients makes the product-residual
            // normal-range proof explicit, including tiny near-axis terms.
            if (coefficient.Sine != 0 && Math.Abs(coefficient.Sine) < minimum
                || coefficient.Cosine != 0 && Math.Abs(coefficient.Cosine) < minimum) return false;
            AccumulateFloating(value, coefficient.Sine, ref realSum, ref realLow, ref realHigh);
            AccumulateFloating(value, coefficient.Cosine, ref imaginarySum, ref imaginaryLow, ref imaginaryHigh);
            if (--index < 0) index = 49;
        }
        double realLower = realSum + realLow, realUpper = realSum + realHigh;
        double imaginaryLower = imaginarySum + imaginaryLow, imaginaryUpper = imaginarySum + imaginaryHigh;
        if (!double.IsFinite(realLower) || !double.IsFinite(imaginaryLower)
            || BitConverter.DoubleToInt64Bits(realLower) != BitConverter.DoubleToInt64Bits(realUpper)
            || BitConverter.DoubleToInt64Bits(imaginaryLower) != BitConverter.DoubleToInt64Bits(imaginaryUpper)) return false;
        real = JurikDyadic.FromDouble(realLower);
        imaginary = JurikDyadic.FromDouble(imaginaryLower);
        return true;
    }

    private static void AccumulateFloating(double value, double coefficient,
        ref double sum, ref double lower, ref double upper)
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
        lower = Math.BitDecrement(Math.BitDecrement(lower + productError) + sumError);
        upper = Math.BitIncrement(Math.BitIncrement(upper + productError) + sumError);
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

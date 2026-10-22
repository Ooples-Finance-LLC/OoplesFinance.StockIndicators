#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Helpers;

// Outward integer bounds for the complete sine/cosine projections. A result
// is accepted only when each pair of endpoints rounds to one finite binary64
// value. Tiny near-axis terms are enclosed, never discarded. Ambiguous phase
// decisions and extended exponents retain the original exact projection.
internal static class CertifiedHilbertProjection
{
    private readonly record struct Factor(long Mantissa, int Shift);
    private readonly record struct Coefficient(Factor Sine, Factor Cosine);
    private static readonly Coefficient[][] Coefficients = CreateCoefficients();

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

    private static void AddProduct(Int128 value, Factor factor, ref Int128 lower, ref Int128 upper)
    {
        if (factor.Mantissa == 0) return;
        Int128 product = value * factor.Mantissa;
        if (factor.Shift >= 127)
        {
            // Any such nonzero product lies strictly between -1 and 1 on
            // the common grid; retain its directed interval and its sign.
            if (product < 0) lower--;
            else if (product > 0) upper++;
            return;
        }
        Int128 floor = product >> factor.Shift;
        lower += floor;
        upper += floor;
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

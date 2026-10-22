#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Helpers;

// Nearby normal prices share an exact integer grid. For windows up to 256,
// their absolute deviations fit an unsigned 64-bit sum. Every other case
// keeps the existing dyadic implementation, including subnormal boundaries.
internal static class CertifiedCommodityChannel
{
    internal static bool TryCompute(Queue<double> prices, double mean, double current,
        out double result, out bool defined)
    {
        result = 0;
        defined = false;
        int count = prices.Count;
        if (count is < 1 or > 256) return false;
        long bits = BitConverter.DoubleToInt64Bits(mean);
        int exponent = (int)((bits >> 52) & 2047);
        if (exponent is 0 or 2047) return false;
        long center = ((bits & 0xfffffffffffffL) | (1L << 52)) << 1;
        if (bits < 0) center = -center;
        int grid = exponent - 1;
        if (!TryAlign(current, grid, out long latest)) return false;
        ulong sum = 0;
        foreach (double price in prices)
        {
            if (!TryAlign(price, grid, out long aligned)) return false;
            // |aligned| < 2^55, |center| < 2^54, so each distance is
            // strictly below 2^56. At most 256 terms cannot overflow UInt64.
            sum = unchecked(sum + (ulong)Math.Abs(aligned - center));
        }
        if (sum == 0) return true;
        long distance = latest - center;
        if (distance == 0) { defined = true; return true; }
        // The common binary exponent cancels. These exact products need at
        // most 72 and 66 bits, respectively; the midpoint certifier rounds once.
        UInt128 numerator = (UInt128)Math.Abs(distance) * (uint)(200 * count);
        UInt128 denominator = (UInt128)sum * 3;
        if (!CertifiedCompactRatio.TryDivide(numerator, denominator, 0, out result)) return false;
        if (distance < 0) result = -result;
        defined = true;
        return true;
    }

    private static bool TryAlign(double value, int grid, out long aligned)
    {
        aligned = 0;
        if (value == 0) return true;
        long bits = BitConverter.DoubleToInt64Bits(value);
        int exponent = (int)((bits >> 52) & 2047);
        int shift = exponent - grid;
        if (exponent is 0 or 2047 || shift is < 0 or > 2) return false;
        aligned = ((bits & 0xfffffffffffffL) | (1L << 52)) << shift;
        if (bits < 0) aligned = -aligned;
        return true;
    }
}
#endif

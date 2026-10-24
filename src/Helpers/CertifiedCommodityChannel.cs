#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Helpers;

// Nearby prices retain the narrow single-pass grid. Wider normal windows,
// including means near zero, use a bounded 128-bit grid before dyadic fallback.
internal static class CertifiedCommodityChannel
{
    internal static bool TryCompute(Queue<double> prices, double mean, double current,
        out double result, out bool defined) =>
        TryComputeNarrow(prices, mean, current, out result, out defined)
        || TryComputeWide(prices, mean, current, out result, out defined);

    private static bool TryComputeNarrow(Queue<double> prices, double mean, double current,
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
        UInt128 numerator = unchecked((UInt128)Math.Abs(distance) * (uint)(200 * count));
        UInt128 denominator = unchecked((UInt128)sum * 3);
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

    private static bool TryComputeWide(Queue<double> prices, double mean, double current,
        out double result, out bool defined)
    {
        result = 0;
        defined = false;
        int count = prices.Count;
        if (count is < 1 or > 256) return false;
        int grid = 2047, maximum = 0;
        if (!Include(mean, ref grid, ref maximum) || !Include(current, ref grid, ref maximum)) return false;
        foreach (double price in prices)
            if (!Include(price, ref grid, ref maximum)) return false;
        if (maximum == 0) return true;
        Int128 center = Align(mean, grid), latest = Align(current, grid);
        UInt128 sum = 0;
        // The exponent span is at most 32: distances stay below 2^86 and the sum below 2^94.
        foreach (double price in prices)
        {
            Int128 distance = unchecked(Align(price, grid) - center);
            sum = unchecked(sum + (UInt128)(distance < 0 ? -distance : distance));
        }
        if (sum == 0) return true;
        Int128 difference = unchecked(latest - center);
        if (difference == 0) { defined = true; return true; }
        // A term has at most 85 magnitude bits (53+32). A distance is below
        // 2^86, the sum of 256 distances below 2^94, and these final products
        // below 2^102 and 2^96. All signed and unsigned operations fit exactly.
        UInt128 numerator = unchecked((UInt128)(difference < 0 ? -difference : difference) * (uint)(200 * count));
        UInt128 denominator = unchecked(sum * 3);
        if (!CertifiedCompactRatio.TryDivide(numerator, denominator, 0, out result)) return false;
        if (difference < 0) result = -result;
        defined = true;
        return true;
    }

    private static bool Include(double value, ref int minimum, ref int maximum)
    {
        if (value == 0) return true;
        int exponent = (int)((BitConverter.DoubleToInt64Bits(value) >> 52) & 2047);
        if (exponent is 0 or 2047) return false;
        minimum = Math.Min(minimum, exponent);
        maximum = Math.Max(maximum, exponent);
        return maximum - minimum <= 32;
    }

    private static Int128 Align(double value, int grid)
    {
        if (value == 0) return 0;
        long bits = BitConverter.DoubleToInt64Bits(value);
        int exponent = (int)((bits >> 52) & 2047);
        Int128 aligned = (Int128)((bits & 0xfffffffffffffL) | (1L << 52)) << (exponent - grid);
        return bits < 0 ? unchecked(-aligned) : aligned;
    }
}
#endif

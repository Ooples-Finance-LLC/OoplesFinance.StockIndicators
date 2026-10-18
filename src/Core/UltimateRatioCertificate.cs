#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Core;

internal static class UltimateRatioCertificate
{
    // M bounds each pressure/range sum. The full weighted numerator, including
    // 100, is at most 700*M^3; the denominator is at most 7*M^3. Accepted operands
    // are exact binary64 integers, so only the final division rounds.
    internal static bool FitsDouble(double integerMaximum, int period)
    {
        long wideBound = (1L << 38) / (2L * period);
        if (!(integerMaximum >= 0 && integerMaximum <= wideBound)) return false;
        UInt128 sumBound = (UInt128)(ulong)integerMaximum * (ulong)(2L * period);
        if (sumBound == 0) sumBound = 1; // Zero-range windows retain denominator one.
        return 700 * sumBound * sumBound * sumBound <= ((UInt128)1 << 53);
    }
}
#endif

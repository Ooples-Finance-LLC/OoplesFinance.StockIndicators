#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Core;

internal static class MoneyFlowIntegerCertificate
{
    // Bounds are magnitudes on common integer grids. Scaling preserves the
    // significand, including integers above 2^53 with zero low bits.
    internal static bool TryGetProduct(double priceBound, double volumeBound, out UInt128 product)
    {
        const double longExclusiveLimit = 9223372036854775808d;
        product = 0;
        if (!(priceBound >= 0 && priceBound < longExclusiveLimit
            && volumeBound >= 0 && volumeBound < longExclusiveLimit)) return false;
        product = (UInt128)(ulong)priceBound * (ulong)volumeBound;
        return true;
    }

    // Callers have validated positive periods. The directional flows partition
    // one P-term window, including signed volumes. Publication promotes Int64
    // sums before multiplying by 100; Int128 sums reserve that headroom here.
    internal static bool FitsInt64(UInt128 product, int period) => product <= (UInt128)long.MaxValue / (uint)period;
    internal static bool FitsInt128(UInt128 product, int period) => product <= (UInt128)Int128.MaxValue / 100 / (uint)period;
}
#endif

#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    // One preceding candle restores previous close; the divisor is not a
    // temporal period. Each worker retains the original one-rounding state.
    private readonly struct ScaledRangeKernel(ScaledTrueRange.State state) : IBoundedKernel<ScaledRangeKernel>
    {
        public static ScaledRangeKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) =>
            new(new(((ScaledTrueRange)indicator).Divisor));
        public double Update(in Bar bar) => state.Update(in bar);
    }
}
#endif

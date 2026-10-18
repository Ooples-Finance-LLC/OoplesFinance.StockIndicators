#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    // Both thresholds use preceding ranges. Replaying the longer window
    // restores exact sums even when a worker chooses a different binary grid
    // or transfers to the general exact state. Larger windows stay scalar.
    private static int RickshawReplayWindow(IIndicator indicator) => indicator is RickshawManCandle candle
        && Math.Max(candle.DojiPeriod, candle.NearPeriod) < 4096
            ? Math.Max(candle.DojiPeriod, candle.NearPeriod) + 1 : 0;

    private readonly struct RickshawWindowKernel(RickshawGridState state) : IBoundedKernel<RickshawWindowKernel>
    {
        public static RickshawWindowKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target)
        {
            var candle = (RickshawManCandle)indicator;
            return new(new RickshawGridState(candle.DojiPeriod, candle.NearPeriod));
        }
        public double Update(in Bar bar) => state.Update(in bar);
    }
}
#endif

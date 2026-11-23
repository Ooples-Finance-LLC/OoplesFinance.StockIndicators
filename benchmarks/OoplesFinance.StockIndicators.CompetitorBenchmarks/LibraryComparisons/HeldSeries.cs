using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

/// <summary>
/// A run's series as the array the run holds, where it holds one. A competitor's function fills the array
/// its caller reads, so an arm of ours that copied its result would be timed and charged for a copy that
/// its competitor never makes. The arrays are only read: a flag series may be shared between runs.
/// </summary>
internal static class HeldSeries
{
    internal static double[] Held(this IIndicatorRun run, IIndicator indicator) => run is IndicatorRun held
        ? held.SeriesArray(IndicatorContract.PrimaryOutput(indicator))
        : run[indicator].ToArray();

    internal static double[] Held(this IIndicatorRun run, IIndicatorOutput output) => run is IndicatorRun held
        ? held.SeriesArray(output)
        : run[output].ToArray();
}

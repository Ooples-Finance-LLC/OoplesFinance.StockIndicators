using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class HilbertCycleReference
{
    internal static double?[][] Values(IReadOnlyList<Bar> bars, bool periodOnly, int suppression)
    {
        var values = SeededPhaseReference.Cycle(bars, suppression);
        return periodOnly ? [values[2]] : [values[0], values[1]];
    }
}

using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static bool HasBoundedMiddleMean(IBuiltInIndicator indicator) =>
        indicator.BatchName == IndicatorName.MiddleHighLowMovingAverage && BoundedMeanKind(indicator.CreateOptions(), 3) is 1 or 2 or 3 or 6 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 14 or 15 or 16 or 17 or 18 or 19 or 20 or 21;

    internal static double[] RoundedMiddleMean(IReadOnlyList<Bar> bars, int smoothing, int window, int kind)
    {
        var midpoint = bars.Select((_, i) =>
        {
            var values = bars.Skip(Math.Max(0, i - window + 1)).Take(Math.Min(window, i + 1)).Select(b => b.Close).ToArray();
            return ExactPriceMean(values.Min(), values.Max());
        }).ToArray();
        return RoundedBoundedStage(midpoint, smoothing, kind);
    }
}

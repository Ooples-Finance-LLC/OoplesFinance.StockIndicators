using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static bool HasBoundedSlowMean(IBuiltInIndicator indicator) =>
        indicator.BatchName == IndicatorName.SlowSmoothedMovingAverage && BoundedMeanKind(indicator.CreateOptions(), 2) is 1 or 2 or 3 or 6 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 14 or 15 or 16 or 17 or 18 or 19;

    internal static double[] RoundedSlowMean(IReadOnlyList<Bar> bars, int length, int kind)
    {
        var period = Math.Max(1L, length);
        var middle = (int)Math.Min(530, (period + 2) / 3);
        var first = (int)Math.Max(1, Math.Min(530, (period - middle + 1) / 2));
        var last = (int)Math.Max(1, Math.Min(530, (period - middle) / 2));
        double[] Stage(IReadOnlyList<double> values, int width) => RoundedBoundedStage(values, width, kind);
        return Stage(Stage(Stage(Closes(bars), first), middle), last);
    }
}

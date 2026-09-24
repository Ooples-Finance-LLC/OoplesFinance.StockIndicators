using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static bool HasBoundedSequentialMean(IBuiltInIndicator indicator) =>
        indicator.BatchName == IndicatorName.SequentiallyFilteredMovingAverage && BoundedMeanKind(indicator.CreateOptions(), 1) is 1 or 2 or 3 or 6 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 14 or 15 or 16 or 17 or 18 or 19 or 20;

    internal static double[] RoundedSequentialMean(IReadOnlyList<Bar> bars, int length, int kind)
    {
        var means = RoundedBoundedStage(Closes(bars), length, kind);
        var result = new double[bars.Count];
        for (var i = 0; i < result.Length; i++)
        {
            var comparisons = Enumerable.Range(Math.Max(0, i - length + 1), Math.Min(length, i + 1))
                .Select(j => means[j].CompareTo(j == 0 ? 0 : means[j - 1])).ToArray();
            var monotone = comparisons.Length == length && (comparisons.All(v => v > 0) || comparisons.All(v => v < 0));
            result[i] = monotone ? means[i] : i == 0 ? bars[i].Close : result[i - 1];
        }
        return result;
    }
}

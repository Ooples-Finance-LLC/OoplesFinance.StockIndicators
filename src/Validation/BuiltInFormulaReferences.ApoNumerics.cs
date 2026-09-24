using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static bool HasRoundedApo(IBuiltInIndicator indicator) => indicator.BatchName == IndicatorName.AbsolutePriceOscillator
        && BoundedMeanKind(indicator.CreateOptions(), 3) is 1 or 2 or 3 or 6 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 14 or 15 or 16 or 17 or 18 or 19 or 20;

    internal static double[] RoundedApo(IReadOnlyList<Bar> bars, int fast, int slow, int kind)
    {
        var values = Closes(bars);
        var first = RoundedBoundedStage(values, fast, kind);
        var second = RoundedBoundedStage(values, slow, kind);
        return first.Select((value, i) => (ReferenceFraction.FromDouble(value)
            - ReferenceFraction.FromDouble(second[i])).ToDouble()).ToArray();
    }
}

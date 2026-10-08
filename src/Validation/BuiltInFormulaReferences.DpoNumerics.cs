using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static bool HasRoundedDpo(IBuiltInIndicator indicator) => indicator.BatchName == IndicatorName.DetrendedPriceOscillator
        && BoundedMeanKind(indicator.CreateOptions(), 1) is 1 or 2 or 3 or 6 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 14 or 15 or 16 or 17 or 18 or 19 or 20 or 21;

    internal static double[] RoundedDpo(IReadOnlyList<Bar> bars, int length, int kind)
    {
        var values = Closes(bars);
        var average = RoundedBoundedStage(values, length, kind);
        var lag = (int)Math.Max(2, Math.Min(530, (length + 1L) / 2 + 1));
        return average.Select((value, i) => (ReferenceFraction.FromDouble(i >= lag ? values[i - lag] : 0)
            - ReferenceFraction.FromDouble(value)).ToDouble()).ToArray();
    }
}

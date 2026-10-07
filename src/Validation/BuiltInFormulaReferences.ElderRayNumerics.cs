using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static bool HasRoundedElderRay(IBuiltInIndicator indicator) => indicator.BatchName == IndicatorName.ElderRayIndex
        && BoundedMeanKind(indicator.CreateOptions(), 3) is 1 or 2 or 3 or 6 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 14 or 15 or 16 or 17 or 18 or 19 or 20 or 21;

    internal static double[] RoundedElderRay(IReadOnlyList<Bar> bars, int length, int kind, bool bull)
    {
        var center = RoundedBoundedStage(Closes(bars), length, kind);
        return bars.Select((bar, i) => (ReferenceFraction.FromDouble(bull ? bar.High : bar.Low)
            - ReferenceFraction.FromDouble(center[i])).ToDouble()).ToArray();
    }
}

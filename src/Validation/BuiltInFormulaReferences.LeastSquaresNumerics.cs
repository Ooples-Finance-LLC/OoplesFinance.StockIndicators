using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoundedLeastSquaresMean(IReadOnlyList<Bar> bars, int length)
    {
        var weighted = ExactWeightedWindow(bars, length);
        var simple = ExactDeviationSignal(Closes(bars), length, 1);
        return weighted.Select((value, i) => (new ReferenceFraction(3) * ReferenceFraction.FromDouble(value)
            - new ReferenceFraction(2) * ReferenceFraction.FromDouble(simple[i])).ToDouble()).ToArray();
    }
}

using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoundedQmaSmaDifference(IReadOnlyList<Bar> bars, int length)
    {
        var rms = RoundedRootMeanSquare(bars, length);
        var mean = RoundedBoundedStage(Closes(bars), length, 1);
        return rms.Select((value, i) => (ReferenceFraction.FromDouble(value) -
            ReferenceFraction.FromDouble(mean[i])).ToDouble()).ToArray();
    }
}

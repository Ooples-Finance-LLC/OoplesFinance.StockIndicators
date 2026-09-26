using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoundedDisparity(IReadOnlyList<Bar> bars, object options)
    {
        var mean = RoundedBoundedStage(Closes(bars), Integer(options, "Length", 14), BoundedMeanKind(options, 1));
        return bars.Select((b, i) => mean[i] == 0 ? 0
            : (new ReferenceFraction(100) * (ReferenceFraction.FromDouble(b.Close) /
                ReferenceFraction.FromDouble(mean[i]) - new ReferenceFraction(1))).ToDouble()).ToArray();
    }
}

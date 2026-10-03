using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> ForceOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator, double[]? customerMeans = null)
    {
        var zero = new ReferenceFraction(0); var options = indicator.CreateOptions();
        var forces = bars.Select((b, i) => i == 0 ? zero : RoundRocBankStage(
            (ReferenceFraction.FromDouble(b.Close) - ReferenceFraction.FromDouble(bars[i - 1].Close)) * ReferenceFraction.FromDouble(b.Volume))).ToArray();
        var mean = customerMeans ?? SmoothRocBankStage(forces, Integer(options, "Length", 14), AverageKind(options, 3)).Select(v => v.ToDouble()).ToArray();
        return Outputs(("Fi", mean));
    }
}

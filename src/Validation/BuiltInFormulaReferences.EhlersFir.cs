using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> EhlersFirOutputs(IReadOnlyList<Bar> bars, double[]? coefficients = null)
    {
        var weights = (coefficients ?? new[] { 1d, 3.5, 4.5, 3, .5, -.5, -1.5 }).Select(ReferenceFraction.FromDouble).ToArray();
        var denominator = weights.Aggregate(new ReferenceFraction(0), (sum, weight) => sum + weight);
        var line = bars.Select((_, i) => (Enumerable.Range(0, Math.Min(i + 1, 7))
            .Aggregate(new ReferenceFraction(0), (sum, lag) => sum + weights[lag] * ReferenceFraction.FromDouble(bars[i - lag].Close)) / denominator).ToDouble()).ToArray();
        return Outputs(("Efirf", line));
    }
}

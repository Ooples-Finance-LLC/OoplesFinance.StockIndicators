using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> AdaptiveRsiOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); var kind = AverageKind(options, 6);
        var line = RoundedPriceRsi(bars, Integer(options, "Length", 14), kind);
        var result = new double[bars.Count];
        for (var i = 0; i < result.Length; i++)
        {
            var scaled = (ReferenceFraction.FromDouble(line[i]) / new ReferenceFraction(100)).ToDouble();
            var alpha = ReferenceFraction.FromDouble(2 * Math.Abs(scaled - 0.5));
            var previous = ReferenceFraction.FromDouble(i == 0 ? 0 : result[i - 1]);
            result[i] = (alpha * ReferenceFraction.FromDouble(bars[i].Close) + (new ReferenceFraction(1) - alpha) * previous).ToDouble();
        }
        return Outputs(("Arsi", result));
    }
}

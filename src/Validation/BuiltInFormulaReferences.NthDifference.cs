using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> NthDifferenceOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator) => NthDifferenceOutputs(bars, Integer(indicator.CreateOptions(), "Length", 14), 2);
    internal static IReadOnlyDictionary<string, double[]> NthDifferenceOutputs(IReadOnlyList<Bar> bars, int lag, int order)
    {
        lag = Math.Max(1, lag); order = Math.Max(0, order); var values = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        // Successive exact lagged differences are independent of the engine's binomial coefficients.
        for (var stage = 0; stage < order; stage++) { var previous = values; values = previous.Select((v,i) => v - (i >= lag ? previous[i - lag] : new ReferenceFraction(0))).ToArray(); }
        return Outputs(("Nodo", values.Select(v => v.ToDouble()).ToArray()));
    }
}

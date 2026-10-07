using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> DmiStochasticOutputs(IReadOnlyList<Bar> bars, int length, int kind)
    {
        // Independently calculated directional stages must match the published ADX components.
        // Tiny changes in a nearly flat directional spread are amplified by stochastic normalization.
        var directional = DirectionalIndexOutputs(bars, length, kind);
        var spread = directional["DiMinus"].Select((value, i) => value - directional["DiPlus"][i]).ToArray();
        var rank = spread.Select((value, i) =>
        {
            var sample = Window(spread, i, 10).ToArray();
            var range = sample.Max() - sample.Min();
            return range == 0 ? 0 : Clamp((value - sample.Min()) / range * 100, 0, 100);
        }).ToArray();
        return Outputs(("DmiStochastic", Average(Average(rank, 3, kind), 3, kind)));
    }
}

using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Builder.Specs;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> ShinoharaOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); var length = Integer(options, "Length", 14);
        var second = options is ShinoharaIntensityRatioBSpecOptions;
        var tops = bars.Select((b, i) => ReferenceFraction.FromDouble(b.High) -
            ReferenceFraction.FromDouble(second ? i == 0 ? 0 : bars[i - 1].Close : b.Open)).ToArray();
        var bottoms = bars.Select((b, i) => ReferenceFraction.FromDouble(second ? i == 0 ? 0 : bars[i - 1].Close : b.Open) -
            ReferenceFraction.FromDouble(b.Low)).ToArray();
        var zero = new ReferenceFraction(0);
        var output = bars.Select((_, i) =>
        {
            var top = Window(tops, i, length).Aggregate(zero, (a, b) => a + b);
            var bottom = Window(bottoms, i, length).Aggregate(zero, (a, b) => a + b);
            return bottom.Sign == 0 ? 0 : (new ReferenceFraction(100) * top / bottom).ToDouble();
        }).ToArray();
        return Outputs((second ? "BRatio" : "ARatio", output));
    }
}

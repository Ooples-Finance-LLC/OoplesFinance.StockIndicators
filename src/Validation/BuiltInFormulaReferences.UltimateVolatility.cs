using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> UltimateVolatilityOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
        => new Dictionary<string, double[]> { ["Uvi"] = UltimateVolatilityValues(bars, Integer(indicator.CreateOptions(), "Length", 14)) };

    internal static double[] UltimateVolatilityValues(IReadOnlyList<Bar> bars, int length)
    {
        length = Math.Max(1, length);
        var output = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var total = new ReferenceFraction(0);
            for (var j = Math.Max(0, i - length + 1); j <= i; j++)
                total += (ReferenceFraction.FromDouble(bars[j].Close) - ReferenceFraction.FromDouble(bars[j].Open)).Abs();
            output[i] = (total / new ReferenceFraction(length)).ToDouble();
        }
        return output;
    }
}

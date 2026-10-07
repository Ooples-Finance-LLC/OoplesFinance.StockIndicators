using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> DampedSineOutputs(IReadOnlyList<Bar> bars, int length)
    {
        length = Math.Max(3, length);
        var weights = Enumerable.Range(1, length).Select(j => ReferenceFraction.FromDouble(Math.Sin(2 * Math.PI * j / length) / j)).ToArray();
        var denominator = weights.Aggregate(new ReferenceFraction(0), (sum, weight) => sum + weight);
        var values = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var sum = new ReferenceFraction(0);
            for (var j = 0; j < length && j <= i; j++) sum += weights[j] * ReferenceFraction.FromDouble(bars[i - j].Close);
            values[i] = (sum / denominator).ToDouble();
        }
        return Outputs(("Dswwf", values));
    }
}

using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> ShapeshiftingOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator) =>
        ShapeshiftingOutputs(bars, Math.Max(2, Integer(indicator.CreateOptions(), "Length", 50)));
    internal static IReadOnlyDictionary<string, double[]> ShapeshiftingOutputs(IReadOnlyList<Bar> bars, int length)
    {
        length = Math.Max(2, length);
        var zero = new ReferenceFraction(0); var one = new ReferenceFraction(1);
        var weights = Enumerable.Range(0, length).Select(j =>
        {
            var x = new ReferenceFraction(j) / new ReferenceFraction(length - 1);
            var weight = one - new ReferenceFraction(2) * x / (one + x * x * x * x);
            return ReferenceFraction.FromDouble(weight.ToDouble());
        }).ToArray();
        var mass = weights.Aggregate(zero, (sum, w) => sum + w);
        var values = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var sum = zero;
            for (var j = 0; j < Math.Min(i + 1, length); j++) sum += weights[j] * ReferenceFraction.FromDouble(bars[i - j].Close);
            values[i] = (sum / mass).ToDouble();
        }
        return Outputs(("Sma", values));
    }
}

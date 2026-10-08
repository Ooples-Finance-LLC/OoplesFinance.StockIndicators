using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> TrendIntensityOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator, int slowLength = 60, double[]? customerMeans = null)
    {
        var options = indicator.CreateOptions(); var length = Integer(options, "Length", 30);
        var source = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        var means = customerMeans is null ? SmoothStrengthStage(source, slowLength, AverageKind(options, 1)) : customerMeans.Select(ReferenceFraction.FromDouble).ToArray();
        var changes = source.Select((v, i) => v - means[i]).ToArray(); var zero = new ReferenceFraction(0);
        var output = changes.Select((_, i) =>
        {
            var current = Window(changes, i, length).ToArray();
            var total = current.Aggregate(zero, (a, b) => a + b.Abs());
            var up = current.Where(v => v.Sign > 0).Aggregate(zero, (a, b) => a + b);
            return total.Sign == 0 ? 0 : (new ReferenceFraction(100) * up / total).ToDouble();
        }).ToArray();
        return Outputs(("Tii", output));
    }
}

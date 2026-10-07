using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RelativeVolumeOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator, double[]? customerMeans = null)
    {
        var options = indicator.CreateOptions();
        var score = StandardizedValues(bars.Select(b => b.Volume).ToArray(), Integer(options, "Length", 60), AverageKind(options, 1), false, customerMeans);
        var demand = new double[bars.Count]; var lastTrigger = -1;
        for (var i = 0; i < bars.Count; i++)
        {
            if (score[i] >= 2) lastTrigger = i;
            demand[i] = lastTrigger < 0 ? bars[0].Close : lastTrigger == 0 ? 0 : bars[lastTrigger - 1].Close;
        }
        return Outputs(("Rvi", score), ("Dpl", demand));
    }
}

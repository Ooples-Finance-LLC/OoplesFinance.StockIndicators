using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> WilliamsAccumulationOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator, double[]? customerSignals = null)
    {
        var total = new ReferenceFraction(0);
        var cumulative = bars.Select((bar, i) =>
        {
            if (i > 0 && bar.Close != bars[i - 1].Close)
            {
                var reference = bar.Close > bars[i - 1].Close ? Math.Min(bar.Low, bars[i - 1].Close) : Math.Max(bar.High, bars[i - 1].Close);
                total += ReferenceFraction.FromDouble(bar.Close) - ReferenceFraction.FromDouble(reference);
            }
            return RoundRocBankStage(total);
        }).ToArray();
        if (indicator.BatchName == IndicatorName.WilliamsAccumulationDistribution) return Outputs(("Wad", cumulative.Select(v => v.ToDouble()).ToArray()));
        var options = indicator.CreateOptions();
        var signal = customerSignals ?? SmoothRocBankStage(cumulative, Integer(options, "Length", 14), AverageKind(options, 1)).Select(v => v.ToDouble()).ToArray();
        return Outputs(("Swad", cumulative.Select(v => v.ToDouble()).ToArray()), ("Signal", signal));
    }
}

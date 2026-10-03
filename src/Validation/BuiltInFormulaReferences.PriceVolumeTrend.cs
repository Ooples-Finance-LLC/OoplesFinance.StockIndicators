using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> PriceVolumeTrendOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator, double[]? customerSignals = null)
    {
        var options = indicator.CreateOptions(); var modified = indicator.BatchName == IndicatorName.ModifiedPriceVolumeTrend;
        var zero = new ReferenceFraction(0); var total = zero; var scale = new ReferenceFraction(modified ? 50000 : 1);
        var line = bars.Select((b, i) =>
        {
            if (i == 0 || bars[i - 1].Close == 0) { if (modified) total = zero; }
            else
            {
                var previous = ReferenceFraction.FromDouble(bars[i - 1].Close);
                total += RoundRocBankStage((ReferenceFraction.FromDouble(b.Close) - previous) * ReferenceFraction.FromDouble(b.Volume) / (previous * scale));
            }
            return RoundRocBankStage(total);
        }).ToArray();
        var signal = customerSignals ?? SmoothRocBankStage(line, Integer(options, "Length", modified ? 23 : 14), AverageKind(options, modified ? 1 : 3)).Select(v => v.ToDouble()).ToArray();
        return Outputs((modified ? "Mpvt" : "Pvt", line.Select(v => v.ToDouble()).ToArray()), ("Signal", signal));
    }
}

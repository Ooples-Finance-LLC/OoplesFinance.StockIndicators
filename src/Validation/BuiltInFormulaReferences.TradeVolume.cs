using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> TradeVolumeOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator, double[]? customerSignals = null, double minTickValue = 0.5)
    {
        var options = indicator.CreateOptions(); var total = new ReferenceFraction(0); var tick = ReferenceFraction.FromDouble(minTickValue);
        var line = bars.Select((b, i) =>
        {
            var difference = ReferenceFraction.FromDouble(b.Close) - ReferenceFraction.FromDouble(i == 0 ? 0 : bars[i - 1].Close);
            if (difference.CompareTo(tick) > 0) total += ReferenceFraction.FromDouble(b.Volume);
            else if (difference.CompareTo(new ReferenceFraction(0) - tick) < 0) total -= ReferenceFraction.FromDouble(b.Volume);
            return RoundRocBankStage(total);
        }).ToArray();
        var signal = customerSignals ?? SmoothRocBankStage(line, Integer(options, "Length", 14), AverageKind(options, 1)).Select(v => v.ToDouble()).ToArray();
        return Outputs(("Tvi", line.Select(v => v.ToDouble()).ToArray()), ("Signal", signal));
    }
}

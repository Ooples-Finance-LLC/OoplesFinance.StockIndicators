using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> TrendDetectionOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator, int? longLength = null)
    {
        var options = indicator.CreateOptions(); var shortPeriod = Integer(options, "Length1", 20); var longPeriod = longLength ?? Integer(options, "Length2", 40);
        var zero = new ReferenceFraction(0);
        var momentum = bars.Select((b, i) => i < shortPeriod ? zero : RoundRocBankStage(ReferenceFraction.FromDouble(b.Close) - ReferenceFraction.FromDouble(bars[i - shortPeriod].Close))).ToArray();
        var line = new double[bars.Count]; var direction = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var recent = momentum.Skip(Math.Max(0, i - shortPeriod + 1)).Take(Math.Min(i + 1, shortPeriod)).ToArray();
            var longer = momentum.Skip(Math.Max(0, i - longPeriod + 1)).Take(Math.Min(i + 1, longPeriod));
            var signed = recent.Aggregate(zero, (sum, v) => sum + v);
            var absoluteShort = recent.Aggregate(zero, (sum, v) => sum + v.Abs());
            var absoluteLong = longer.Aggregate(zero, (sum, v) => sum + v.Abs());
            direction[i] = signed.ToDouble(); line[i] = (signed.Abs() + absoluteShort - absoluteLong).ToDouble();
        }
        return Outputs(("Tdi", line), ("TdiDirection", direction));
    }
}

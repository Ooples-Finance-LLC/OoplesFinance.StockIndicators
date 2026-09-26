using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> StochasticCustomOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator, int firstLength = 3, int signalLength = 12)
    {
        var options = indicator.CreateOptions(); var rangeLength = Integer(options, "Length", 7);
        var kind = AverageKind(options, 1);
        var distance = new ReferenceFraction[bars.Count]; var range = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var window = bars.Skip(Math.Max(0, i - rangeLength + 1)).Take(Math.Min(i + 1, rangeLength));
            var high = window.Max(b => b.High); var low = window.Min(b => b.Low);
            distance[i] = RoundRocBankStage(ReferenceFraction.FromDouble(bars[i].Close) - ReferenceFraction.FromDouble(low));
            range[i] = RoundRocBankStage(ReferenceFraction.FromDouble(high) - ReferenceFraction.FromDouble(low));
        }
        var d = SmoothRocBankStage(distance, firstLength, kind);
        var r = SmoothRocBankStage(range, firstLength, kind);
        var line = d.Select((v, i) => r[i].Sign == 0 ? 0 : Math.Clamp((new ReferenceFraction(100) * v / r[i]).ToDouble(), 0, 100)).ToArray();
        var signal = SmoothRocBankStage(line.Select(ReferenceFraction.FromDouble).ToArray(), signalLength, kind).Select(v => v.ToDouble()).ToArray();
        return Outputs(("Sco", line), ("Signal", signal));
    }
}

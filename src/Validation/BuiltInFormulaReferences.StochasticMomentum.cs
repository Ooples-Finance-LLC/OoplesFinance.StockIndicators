using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> StochasticMomentumOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); var rangeLength = Integer(options, "Length1", 2); var firstLength = Integer(options, "Length2", 8);
        var secondLength = Integer(options, "SmoothLength1", 5); var signalLength = Integer(options, "SmoothLength2", 5); var kind = AverageKind(options, 3);
        var distance = new ReferenceFraction[bars.Count]; var range = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var window = bars.Skip(Math.Max(0, i - rangeLength + 1)).Take(Math.Min(i + 1, rangeLength));
            var high = window.Max(b => b.High); var low = window.Min(b => b.Low);
            distance[i] = RoundRocBankStage(ReferenceFraction.FromDouble(bars[i].Close) - ReferenceFraction.FromDouble(ExactPriceMean(high, low)));
            range[i] = RoundRocBankStage(ReferenceFraction.FromDouble(high) - ReferenceFraction.FromDouble(low));
        }
        var d = SmoothRocBankStage(SmoothRocBankStage(distance, firstLength, kind), secondLength, kind);
        var r = SmoothRocBankStage(SmoothRocBankStage(range, firstLength, kind), secondLength, kind);
        var line = d.Select((v, i) => r[i].Sign == 0 ? 0 : Math.Clamp((new ReferenceFraction(200) * v / r[i]).ToDouble(), -100, 100)).ToArray();
        var signal = SmoothRocBankStage(line.Select(ReferenceFraction.FromDouble).ToArray(), signalLength, kind).Select(v => v.ToDouble()).ToArray();
        return Outputs(("Smi", line), ("Signal", signal));
    }
}

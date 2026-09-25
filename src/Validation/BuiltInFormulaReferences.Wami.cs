using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> WamiOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        var length = Integer(options, "Length", 13);
        var kind = AverageKind(options, 3);
        var changes = bars.Select((bar, i) => i == 0 ? new ReferenceFraction(0) :
            RoundStrengthStage(ReferenceFraction.FromDouble(bar.Close) - ReferenceFraction.FromDouble(bars[i - 1].Close))).ToArray();
        var weighted = SmoothStrengthStage(changes, 4, 2);
        var first = SmoothStrengthStage(weighted, length, kind);
        return Outputs(("Wami", SmoothStrengthStage(first, length, kind).Select(v => v.ToDouble()).ToArray()));
    }
}

using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> SelfAdjustingRsiOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); var kind = AverageKind(options, 1); var length = Integer(options, "Length", 14);
        var line = RoundedPriceRsi(bars, length, kind);
        var signal = SmoothStrengthStage(line.Select(ReferenceFraction.FromDouble).ToArray(), Integer(options, "SmoothingLength", 21), kind).Select(v => v.ToDouble()).ToArray();
        var mult = ReferenceFraction.FromDouble(Number(options, 2, "Mult"));
        var widths = PopulationDeviation(line, length).Select(v => (mult * ReferenceFraction.FromDouble(v)).ToDouble()).ToArray();
        return Outputs(("SaRsi", line), ("Signal", signal), ("ObLevel", widths.Select(v => 50 + v).ToArray()), ("OsLevel", widths.Select(v => 50 - v).ToArray()));
    }
}

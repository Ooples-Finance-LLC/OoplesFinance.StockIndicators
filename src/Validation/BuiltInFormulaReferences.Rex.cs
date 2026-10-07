using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RexOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator) { var o = indicator.CreateOptions(); return RexOutputs(bars, Integer(o, "Length", 14), AverageKind(o, 3)); }
    internal static IReadOnlyDictionary<string, double[]> RexOutputs(IReadOnlyList<Bar> bars, int length, int kind, double[][]? external = null)
    {
        var input = bars.Select(b=>RoundRocBankStage(new ReferenceFraction(3) * ReferenceFraction.FromDouble(b.Close) - ReferenceFraction.FromDouble(b.Open) - ReferenceFraction.FromDouble(b.High) - ReferenceFraction.FromDouble(b.Low))).ToArray(); var first = external is null ? SmoothRocBankStage(input, Math.Max(1,length), kind) : external[0].Select(ReferenceFraction.FromDouble).ToArray(); var signal = external is null ? SmoothRocBankStage(first, Math.Max(1,length), kind) : external[1].Select(ReferenceFraction.FromDouble).ToArray(); return Outputs(("Ro",first.Select(v=>v.ToDouble()).ToArray()),("Signal",signal.Select(v=>v.ToDouble()).ToArray()));
    }
}

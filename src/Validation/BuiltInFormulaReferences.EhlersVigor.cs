using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> EhlersVigorOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator) { var o = indicator.CreateOptions(); return EhlersVigorOutputs(bars, Integer(o, "Length", 10), AverageKind(o, 1), Integer(o, "SignalLength", 4)); }
    internal static IReadOnlyDictionary<string, double[]> EhlersVigorOutputs(IReadOnlyList<Bar> bars, int length, int kind, int signalLength, double[][]? external = null)
    {
        var input = bars.Select(b=>b.High == b.Low ? new ReferenceFraction(0) : RoundRocBankStage((ReferenceFraction.FromDouble(b.Close) - ReferenceFraction.FromDouble(b.Open)) / (ReferenceFraction.FromDouble(b.High) - ReferenceFraction.FromDouble(b.Low)))).ToArray(); var first = external is null ? SmoothRocBankStage(input, Math.Max(1,length), kind) : external[0].Select(ReferenceFraction.FromDouble).ToArray(); var signal = external is null ? SmoothRocBankStage(first, Math.Max(1,signalLength), kind) : external[1].Select(ReferenceFraction.FromDouble).ToArray(); return Outputs(("Ervi",first.Select(v=>v.ToDouble()).ToArray()),("Signal",signal.Select(v=>v.ToDouble()).ToArray()));
    }
}

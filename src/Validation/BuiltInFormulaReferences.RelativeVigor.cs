using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RelativeVigorOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var o = indicator.CreateOptions(); return RelativeVigorOutputs(bars, Integer(o, "Length", 14), AverageKind(o, 1)); }
    internal static IReadOnlyDictionary<string, double[]> RelativeVigorOutputs(IReadOnlyList<Bar> bars, int length, int kind, double[][]? external = null)
    {
        var zero = new ReferenceFraction(0);
        ReferenceFraction[] Filter(ReferenceFraction[] values) => values.Select((v,i) => RoundRocBankStage((v + (i > 0 ? new ReferenceFraction(2) * values[i-1] : zero) + (i > 1 ? new ReferenceFraction(2) * values[i-2] : zero) + (i > 2 ? values[i-3] : zero)) / new ReferenceFraction(6))).ToArray();
        var numerator = Filter(bars.Select(b=>ReferenceFraction.FromDouble(b.Close)-ReferenceFraction.FromDouble(b.Open)).ToArray()); var denominator = Filter(bars.Select(b=>ReferenceFraction.FromDouble(b.High)-ReferenceFraction.FromDouble(b.Low)).ToArray());
        var n = external is null ? SmoothRocBankStage(numerator, Math.Max(1,length), kind) : external[0].Select(ReferenceFraction.FromDouble).ToArray(); var d = external is null ? SmoothRocBankStage(denominator, Math.Max(1,length), kind) : external[1].Select(ReferenceFraction.FromDouble).ToArray(); var values = n.Select((v,i)=>d[i].Sign==0?zero:RoundRocBankStage(v/d[i])).ToArray(); return Outputs(("Rvi",values.Select(v=>v.ToDouble()).ToArray()),("Signal",Filter(values).Select(v=>v.ToDouble()).ToArray()));
    }
}

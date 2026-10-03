using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> HurstBandOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var options = indicator.CreateOptions(); return HurstBandOutputs(bars, Math.Max(1, Integer(options, "Length", 10)), Number(options, 1.6, "InnerMult"), Number(options, 2.6, "OuterMult"), Number(options, 4.2, "ExtremeMult")); }
    internal static IReadOnlyDictionary<string, double[]> HurstBandOutputs(IReadOnlyList<Bar> bars, int length, double inner, double outer, double extreme)
    {
        length = Math.Max(1, length); var delay = Math.Max(2, Math.Min(530, (int)Math.Ceiling(length / 2d) + 1));
        var delayed = bars.Select((b, i) => i < delay ? new ReferenceFraction(0) : ReferenceFraction.FromDouble(bars[i - delay].Close)).ToArray();
        var middle = delayed.Select((_, i) => { var count = Math.Min(length, i + 1); var sum = new ReferenceFraction(0); foreach (var price in delayed.Skip(i - count + 1).Take(count)) sum += price; return (sum / new ReferenceFraction(count)).ToDouble(); }).ToArray();
        double[] Band(double factor) => middle.Select(v => (ReferenceFraction.FromDouble(v) * (new ReferenceFraction(100) + ReferenceFraction.FromDouble(factor)) / new ReferenceFraction(100)).ToDouble()).ToArray();
        return Outputs(("UpperExtremeBand", Band(extreme)), ("UpperOuterBand", Band(outer)), ("UpperInnerBand", Band(inner)), ("MiddleBand", middle), ("LowerExtremeBand", Band(-extreme)), ("LowerOuterBand", Band(-outer)), ("LowerInnerBand", Band(-inner)));
    }
}

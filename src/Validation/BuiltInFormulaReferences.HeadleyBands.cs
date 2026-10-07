using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> HeadleyBandOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); return HeadleyBandOutputs(bars, Math.Max(1, Integer(options, "Length", 20)), AverageKind(options, 1), Number(options, .001, "Factor"));
    }
    internal static IReadOnlyDictionary<string, double[]> HeadleyBandOutputs(IReadOnlyList<Bar> bars, int length, int kind, double factor)
    {
        var multiplier = ReferenceFraction.FromDouble(factor) * new ReferenceFraction(4000);
        var stages = bars.Select(b =>
        {
            var high = ReferenceFraction.FromDouble(b.High); var low = ReferenceFraction.FromDouble(b.Low); var total = high + low;
            var shift = total.CompareTo(new ReferenceFraction(0)) == 0 ? new ReferenceFraction(0) : (multiplier * (high - low) / total).RoundExtendedBinary64();
            return ((high + high * shift).RoundExtendedBinary64(), (low - low * shift).RoundExtendedBinary64());
        }).ToArray();
        return Outputs(("UpperBand", SmoothRocBankStage(stages.Select(v => v.Item1).ToArray(), length, kind).Select(v => v.ToDouble()).ToArray()),
            ("MiddleBand", SmoothRocBankStage(bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray(), length, kind).Select(v => v.ToDouble()).ToArray()),
            ("LowerBand", SmoothRocBankStage(stages.Select(v => v.Item2).ToArray(), length, kind).Select(v => v.ToDouble()).ToArray()));
    }
}

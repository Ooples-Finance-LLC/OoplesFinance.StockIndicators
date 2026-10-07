using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> PriceAverageChannelOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); var length = Math.Max(1, Integer(options, "Length", 20)); var kind = AverageKind(options, 1);
        var upper = SmoothRocBankStage(bars.Select(b => ReferenceFraction.FromDouble(b.High)).ToArray(), length, kind);
        var lower = SmoothRocBankStage(bars.Select(b => ReferenceFraction.FromDouble(b.Low)).ToArray(), length, kind);
        return Outputs(("UpperBand", upper.Select(v => v.ToDouble()).ToArray()),
            ("MiddleBand", upper.Select((v, i) => ((v + lower[i]) / new ReferenceFraction(2)).ToDouble()).ToArray()),
            ("LowerBand", lower.Select(v => v.ToDouble()).ToArray()));
    }
}

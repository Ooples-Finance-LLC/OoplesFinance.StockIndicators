using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> GeneralizedDoubleOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); var length = Integer(options, "Length", 5); var kind = AverageKind(options, 3);
        var factor = ReferenceFraction.FromDouble(Number(options, .7, "Factor", "VolumeFactor"));
        var first = SmoothRocBankStage(bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray(), length, kind);
        var second = SmoothRocBankStage(first, length, kind);
        return Outputs(("Gdema", first.Select((v, i) => (v + factor * (v - second[i])).ToDouble()).ToArray()));
    }
}

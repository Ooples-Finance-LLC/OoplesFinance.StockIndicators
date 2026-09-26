using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> SupportResistanceOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); var length = Math.Max(1, Integer(options, "Length", 10)); var kind = AverageKind(options, 1);
        var scale = new ReferenceFraction(1) + ReferenceFraction.FromDouble(Number(options, 2, "Factor")) / new ReferenceFraction(100);
        var middle = SmoothRocBankStage(bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray(), length, kind);
        return Outputs(("UpperBand", middle.Select(v => (v * scale).ToDouble()).ToArray()), ("MiddleBand", middle.Select(v => v.ToDouble()).ToArray()),
            ("LowerBand", middle.Select(v => scale.Sign == 0 ? 0 : (v / scale).ToDouble()).ToArray()));
    }
}

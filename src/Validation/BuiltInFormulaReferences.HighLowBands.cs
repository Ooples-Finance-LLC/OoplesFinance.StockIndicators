using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> HighLowBandsOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); var length = Math.Max(1, Integer(options, "Length", 14)); var kind = AverageKind(options, 1);
        var percent = ReferenceFraction.FromDouble(Number(options, 1, "PctShift")); var hundred = new ReferenceFraction(100);
        var first = SmoothRocBankStage(bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray(), length, kind);
        var middle = SmoothRocBankStage(first, length, kind);
        return Outputs(("UpperBand", middle.Select(v => (v * (hundred + percent) / hundred).ToDouble()).ToArray()),
            ("MiddleBand", middle.Select(v => v.ToDouble()).ToArray()),
            ("LowerBand", middle.Select(v => (v * (hundred - percent) / hundred).ToDouble()).ToArray()));
    }
}

using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> OpenCloseHistogramOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); var length = Integer(options, "Length", 10); var kind = AverageKind(options, 3);
        var open = SmoothRocBankStage(bars.Select(b => ReferenceFraction.FromDouble(b.Open)).ToArray(), length, kind);
        var close = SmoothRocBankStage(bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray(), length, kind);
        return Outputs(("OcHistogram", close.Select((value, i) => (value - open[i]).ToDouble()).ToArray()));
    }
}

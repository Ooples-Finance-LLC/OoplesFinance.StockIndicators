using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> McNichollOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); var length = Math.Max(2, Integer(options, "Length", 20)); var kind = AverageKind(options, 3);
        var alpha = new ReferenceFraction(2) / new ReferenceFraction((long)length + 1);
        var first = SmoothRocBankStage(bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray(), length, kind);
        var second = SmoothRocBankStage(first, length, kind);
        return Outputs(("Mnma", first.Select((v, i) => (((new ReferenceFraction(2) - alpha) * v - second[i]) / (new ReferenceFraction(1) - alpha)).ToDouble()).ToArray()));
    }
}

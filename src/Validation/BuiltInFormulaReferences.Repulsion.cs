using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RepulsionOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); var length = Math.Max(1, Integer(options, "Length", 100)); var kind = AverageKind(options, 1);
        return RepulsionOutputs(bars, length, kind);
    }
    internal static IReadOnlyDictionary<string, double[]> RepulsionOutputs(IReadOnlyList<Bar> bars, int length, int kind)
    {
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        var first = SmoothRocBankStage(prices, length, kind);
        var second = SmoothRocBankStage(prices, (int)Math.Min(int.MaxValue, 2L * length), kind);
        var third = SmoothRocBankStage(prices, (int)Math.Min(int.MaxValue, 3L * length), kind);
        return Outputs(("Rma", first.Select((v, i) => (third[i] + second[i] - v).ToDouble()).ToArray()));
    }
}

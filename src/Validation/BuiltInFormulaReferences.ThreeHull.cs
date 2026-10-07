using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> ThreeHullOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); var length = Math.Max(1, Integer(options, "Length", 50)); var kind = AverageKind(options, 2);
        var p = (length + 1L) / 2; var p1 = (p + 2) / 3; var p2 = (p + 1) / 2;
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        var first = SmoothRocBankStage(prices, (int)p1, kind); var second = SmoothRocBankStage(prices, (int)p2, kind);
        var third = SmoothRocBankStage(prices, (int)p, kind);
        var adjusted = first.Select((v, i) => RoundRocBankStage(new ReferenceFraction(3) * v - second[i] - third[i])).ToArray();
        return Outputs(("3hma", SmoothRocBankStage(adjusted, (int)p, kind).Select(v => v.ToDouble()).ToArray()));
    }
}

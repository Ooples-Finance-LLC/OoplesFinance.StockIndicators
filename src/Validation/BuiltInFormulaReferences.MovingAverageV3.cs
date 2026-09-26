using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> MovingAverageV3Outputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator, int length2 = 3)
    {
        var options = indicator.CreateOptions(); var length1 = Math.Max(1, Integer(options, "Length1", Integer(options, "Length", 14))); length2 = Math.Max(1, length2);
        var kind = AverageKind(options, 3); var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        var first = SmoothRocBankStage(prices, length1, kind); var second = SmoothRocBankStage(prices, length2, kind);
        var lambda = new ReferenceFraction(length1) / new ReferenceFraction(length2);
        var alpha = length2 == 1 ? new ReferenceFraction(0) : lambda * new ReferenceFraction(length1 - 1) / (new ReferenceFraction(length1) - lambda);
        return Outputs(("Mav3", first.Select((v, i) => (v + alpha * (v - second[i])).ToDouble()).ToArray()));
    }
}

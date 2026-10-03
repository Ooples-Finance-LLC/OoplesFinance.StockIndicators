using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> EhlersZeroLagOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); var length = Math.Max(1, Integer(options, "Length", 14)); var lag = (length - 1) / 2;
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        var adjusted = prices.Select((price, i) => i < lag ? price : RoundRocBankStage(price + RoundRocBankStage(price - prices[i - lag]))).ToArray();
        return Outputs(("Ezlema", SmoothRocBankStage(adjusted, length, AverageKind(options, 3)).Select(v => v.ToDouble()).ToArray()));
    }
}

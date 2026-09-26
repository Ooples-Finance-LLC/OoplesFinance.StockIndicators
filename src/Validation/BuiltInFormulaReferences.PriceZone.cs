using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> PriceZoneOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator, double[]? customerPrices = null, double[]? customerDirections = null)
    {
        var options = indicator.CreateOptions(); var length = Integer(options, "Length", 20); var kind = AverageKind(options, 3);
        var price = customerPrices ?? SmoothRocBankStage(bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray(), length, kind).Select(v => v.ToDouble()).ToArray();
        var signed = bars.Select((b, i) => ReferenceFraction.FromDouble(i == 0 ? 0 : (b.Close > bars[i - 1].Close ? 1 : b.Close < bars[i - 1].Close ? -1 : 0) * b.Close)).ToArray();
        var directional = customerDirections ?? SmoothRocBankStage(signed, length, kind).Select(v => v.ToDouble()).ToArray();
        var result = price.Select((v, i) => v == 0 ? 0 : Math.Clamp((new ReferenceFraction(100) * ReferenceFraction.FromDouble(directional[i]) / ReferenceFraction.FromDouble(v)).ToDouble(), -100, 100)).ToArray();
        return Outputs(("Pzo", result));
    }
}

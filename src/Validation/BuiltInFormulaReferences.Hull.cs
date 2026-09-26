using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> HullOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); var length = Math.Max(1, Integer(options, "Length", 20)); var kind = AverageKind(options, 2);
        var halfLength = Math.Max(1, Math.Min(530, (int)Math.Round(length / 2d)));
        var rootLength = Math.Max(1, Math.Min(530, (int)Math.Round(Math.Sqrt(length))));
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        var full = SmoothRocBankStage(prices, length, kind); var half = SmoothRocBankStage(prices, halfLength, kind);
        var adjusted = full.Select((v, i) => RoundRocBankStage(new ReferenceFraction(2) * half[i] - v)).ToArray();
        return Outputs(("Hma", SmoothRocBankStage(adjusted, rootLength, kind).Select(v => v.ToDouble()).ToArray()));
    }
}

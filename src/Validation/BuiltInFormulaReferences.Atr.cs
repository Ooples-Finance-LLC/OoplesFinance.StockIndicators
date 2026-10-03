using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> AtrOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); return AtrOutputs(bars, Math.Max(1, Integer(options, "Length", 14)), AverageKind(options, 6));
    }
    internal static IReadOnlyDictionary<string, double[]> AtrOutputs(IReadOnlyList<Bar> bars, int length, int kind)
    {
        var closes = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        var ranges = bars.Select((b, i) =>
        {
            var high = ReferenceFraction.FromDouble(b.High); var low = ReferenceFraction.FromDouble(b.Low); var previous = closes[i == 0 ? 0 : i - 1];
            return new[] { high - low, (high - previous).Abs(), (low - previous).Abs() }.Max().RoundExtendedBinary64();
        }).ToArray();
        return Outputs(("Atr", SmoothRocBankStage(ranges, length, kind).Select(v => v.ToDouble()).ToArray()));
    }
}

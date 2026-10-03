using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> DonchianWidthOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); return DonchianWidthOutputs(bars, Math.Max(1, Integer(options, "Length", 20)), 22, AverageKind(options, 1));
    }
    internal static IReadOnlyDictionary<string, double[]> DonchianWidthOutputs(IReadOnlyList<Bar> bars, int length, int smoothLength, int kind)
    {
        var widths = bars.Select((_, i) =>
        {
            var window = bars.Skip(Math.Max(0, i - length + 1)).Take(Math.Min(i + 1, length)).ToArray();
            return (ReferenceFraction.FromDouble(window.Max(b => b.High)) - ReferenceFraction.FromDouble(window.Min(b => b.Low))).RoundExtendedBinary64();
        }).ToArray();
        return Outputs(("Dcw", widths.Select(v => v.ToDouble()).ToArray()), ("Signal", SmoothRocBankStage(widths, smoothLength, kind).Select(v => v.ToDouble()).ToArray()));
    }
}

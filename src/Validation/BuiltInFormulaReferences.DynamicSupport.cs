using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> DynamicSupportOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); return DynamicSupportOutputs(bars, Math.Max(1, Integer(options, "Length", 25)), AverageKind(options, 6));
    }
    internal static IReadOnlyDictionary<string, double[]> DynamicSupportOutputs(IReadOnlyList<Bar> bars, int length, int kind)
    {
        var ranges = bars.Select((b, i) =>
        {
            var high = ReferenceFraction.FromDouble(b.High); var low = ReferenceFraction.FromDouble(b.Low); var previous = ReferenceFraction.FromDouble(bars[i == 0 ? 0 : i - 1].Close);
            return new[] { high - low, (high - previous).Abs(), (low - previous).Abs() }.Max().RoundExtendedBinary64();
        }).ToArray();
        var atr = SmoothRocBankStage(ranges, length, kind); var multiplier = ReferenceFraction.FromDouble(Math.Sqrt(length));
        var support = new double[bars.Count]; var resistance = new double[bars.Count]; var middle = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var window = bars.Skip(Math.Max(0, i - length + 1)).Take(Math.Min(i + 1, length)).ToArray();
            var highest = ReferenceFraction.FromDouble(window.Max(b => b.High)); var lowest = ReferenceFraction.FromDouble(window.Min(b => b.Low));
            support[i] = (highest - multiplier * atr[i]).ToDouble(); resistance[i] = (lowest + multiplier * atr[i]).ToDouble();
            middle[i] = ((highest + lowest) / new ReferenceFraction(2)).ToDouble();
        }
        return Outputs(("Support", support), ("Resistance", resistance), ("MiddleBand", middle));
    }
}

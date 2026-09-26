using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> AtrDerivedOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); var all = AtrDerivedOutputs(bars, Math.Max(1, Integer(options, "Length", 14)), Number(options, 2, "Multiplier"));
        var key = indicator.BatchName == IndicatorName.AtrChannelWidth ? "Acw" : "Natr"; return Outputs((key, all[key]));
    }
    internal static IReadOnlyDictionary<string, double[]> AtrDerivedOutputs(IReadOnlyList<Bar> bars, int length, double multiplier)
    {
        var close = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        var ranges = bars.Select((b, i) =>
        {
            var high = ReferenceFraction.FromDouble(b.High); var low = ReferenceFraction.FromDouble(b.Low); var previous = close[i == 0 ? 0 : i - 1];
            return new[] { high - low, (high - previous).Abs(), (low - previous).Abs() }.Max().RoundExtendedBinary64();
        }).ToArray();
        var atr = SmoothRocBankStage(ranges, length, 6); var factor = ReferenceFraction.FromDouble(multiplier);
        return Outputs(("Acw", atr.Select(v => (new ReferenceFraction(2) * factor * v).ToDouble()).ToArray()),
            ("Natr", atr.Select((v, i) => close[i].Sign == 0 ? 0 : (new ReferenceFraction(100) * v / close[i]).ToDouble()).ToArray()));
    }
}

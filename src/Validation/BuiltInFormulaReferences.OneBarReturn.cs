using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> OneBarReturnOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var o = indicator.CreateOptions(); var cumulative = indicator.BatchName == IndicatorName.PercentChangeOscillator; return OneBarReturnOutputs(bars, cumulative, Integer(o, "Length", cumulative ? 14 : 3), AverageKind(o, cumulative ? 2 : 1)); }
    internal static IReadOnlyDictionary<string, double[]> OneBarReturnOutputs(IReadOnlyList<Bar> bars, bool cumulative, int length, int kind, double[]? external = null)
    {
        var zero = new ReferenceFraction(0); var values = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var current = ReferenceFraction.FromDouble(bars[i].Close); var previous = i == 0 ? zero : ReferenceFraction.FromDouble(bars[i - 1].Close); var divisor = cumulative ? previous : current;
            var change = i == 0 || divisor.Sign == 0 ? zero : ((current - previous) * new ReferenceFraction(cumulative ? 1 : 100) / divisor).RoundExtendedBinary64();
            values[i] = cumulative && i > 0 && divisor.Sign != 0 ? (values[i - 1] + change).RoundExtendedBinary64() : change;
        }
        var signal = external ?? SmoothRocBankStage(values, Math.Max(1, length), kind).Select(v => v.ToDouble()).ToArray();
        return Outputs((cumulative ? "Pcco" : "Fo", values.Select(v => v.ToDouble()).ToArray()), ("Signal", signal));
    }
}

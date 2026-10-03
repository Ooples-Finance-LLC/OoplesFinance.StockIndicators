using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> TrueRangeAdjustedOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var o = indicator.CreateOptions(); return new Dictionary<string, double[]> { ["Trema"] = TrueRangeAdjustedValues(bars, Integer(o, "Length", 14), Number(o, 1.5, "Mult")).Line }; }
    internal static (double[] Line, Signal[] Signals) TrueRangeAdjustedValues(IReadOnlyList<Bar> bars, int length, double mult)
    {
        length = Math.Max(1, length); ReferenceFraction R(double x) => ReferenceFraction.FromDouble(x); var zero = R(0);
        var ranges = new ReferenceFraction[bars.Count]; var averages = new ReferenceFraction[bars.Count]; var line = new ReferenceFraction[bars.Count]; var signals = new Signal[bars.Count];
        ReferenceFraction Abs(ReferenceFraction x) => x.Sign < 0 ? zero - x : x;
        for (var i = 0; i < bars.Count; i++)
        {
            var previousPrice = R(bars[i == 0 ? 0 : i - 1].Close); var candidates = new[] { R(bars[i].High) - R(bars[i].Low), Abs(R(bars[i].High) - previousPrice), Abs(R(bars[i].Low) - previousPrice) };
            ranges[i] = candidates.Aggregate((a, b) => a.CompareTo(b) >= 0 ? a : b);
            if (i < length) { var sum = zero; for (var j = 0; j <= i; j++) sum += ranges[j]; averages[i] = sum / R(i + 1); }
            else { var alpha = R(2) / R(length + 1L); averages[i] = (R(1) - alpha) * averages[i - 1] + alpha * ranges[i]; }
            var relative = averages[i].Sign == 0 ? R(1) : ranges[i] / averages[i]; var scaled = R(mult) * relative;
            var gain = R(2) * (scaled.CompareTo(R(2)) > 0 ? R(2) : scaled) / R(length + 1L);
            line[i] = i == 0 ? R(bars[i].Close) : (R(1) - gain) * line[i - 1] + gain * R(bars[i].Close);
            var slope = line[i] - (i == 0 ? zero : line[i - 1]); var previous = i == 0 ? zero : line[i - 1] - (i < 2 ? zero : line[i - 2]);
            signals[i] = slope.Sign > 0 && slope.CompareTo(previous) > 0 ? Signal.StrongBuy : slope.Sign < 0 && slope.CompareTo(previous) < 0 ? Signal.StrongSell
                : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None;
        }
        return (line.Select(v => v.ToDouble()).ToArray(), signals);
    }
}

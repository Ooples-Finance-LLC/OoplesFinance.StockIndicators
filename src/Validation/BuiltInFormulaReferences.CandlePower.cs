using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> CandlePowerOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var o = indicator.CreateOptions(); return CandlePowerOutputs(bars, indicator.BatchName == IndicatorName.BullPowerIndicator, Integer(o, "Length", 14), AverageKind(o, 3)); }
    internal static IReadOnlyDictionary<string, double[]> CandlePowerOutputs(IReadOnlyList<Bar> bars, bool bull, int length, int kind, double[]? external = null)
    {
        var values = new ReferenceFraction[bars.Count]; var zero = new ReferenceFraction(0); ReferenceFraction F(double v) => ReferenceFraction.FromDouble(v); ReferenceFraction Max(ReferenceFraction a, ReferenceFraction b) => a.CompareTo(b) >= 0 ? a : b;
        for (var i = 0; i < bars.Count; i++)
        {
            var c = F(bars[i].Close); var o = F(bars[i].Open); var h = F(bars[i].High); var l = F(bars[i].Low); var p = i == 0 ? zero : F(bars[i - 1].Close); var direction = c.CompareTo(o); var gap = p.CompareTo(o); var top = h - c; var bottom = c - l; var range = h - l; ReferenceFraction power;
            if (bull)
            {
                if (direction < 0) power = Max(h - o, bottom);
                else if (gap < 0) power = Max(h - p, bottom);
                else if (direction > 0) power = Max(o - p, range);
                else if (gap > 0) power = range;
                else power = top.CompareTo(bottom) > 0 ? h - o : top.CompareTo(bottom) < 0 ? Max(zero, range) : range;
            }
            else
            {
                if (direction < 0) power = range;
                else if (gap > 0) power = Max(c - o, range);
                else if (direction > 0) power = Max(o - l, top);
                else power = top.CompareTo(bottom) > 0 ? range : top.CompareTo(bottom) < 0 ? o - l : gap < 0 ? Max(o - l, top) : range;
            }
            values[i] = power.RoundExtendedBinary64();
        }
        var signal = external ?? SmoothRocBankStage(values, Math.Max(1, length), kind).Select(v => v.ToDouble()).ToArray();
        return Outputs((bull ? "BullPower" : "BearPower", values.Select(v => v.ToDouble()).ToArray()), ("Signal", signal));
    }
}

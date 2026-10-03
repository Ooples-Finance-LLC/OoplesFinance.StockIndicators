using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> HawkeyeOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator, bool selected = false)
    {
        var options = indicator.CreateOptions();
        return HawkeyeValues(bars, Integer(options, "Length", 200), Number(options, 3.6, "Divisor"), selected).Outputs;
    }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) HawkeyeValues(IReadOnlyList<Bar> bars, int length, double divisor, bool selected = false)
    {
        length = Math.Max(1, length); ReferenceFraction R(double x) => ReferenceFraction.FromDouble(x);
        var highs = new ReferenceFraction[bars.Count]; var lows = new ReferenceFraction[bars.Count]; var mids = new ReferenceFraction[bars.Count];
        var up = new double[bars.Count]; var down = new double[bars.Count]; var signals = new Signal[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var b = bars[i]; var h = b.High; var l = b.Low;
            if (selected && (b.Close > h || b.Close < l)) { var previous = i == 0 ? b.Close : bars[i - 1].Close; h = Math.Max(previous, b.Close); l = Math.Min(previous, b.Close); }
            highs[i] = R(h); lows[i] = R(l); mids[i] = selected ? R(b.Close) : R(((R(h) + R(l)) / R(2)).ToDouble());
            var oldHigh = i == 0 ? R(0) : highs[i - 1]; var oldLow = i == 0 ? R(0) : lows[i - 1]; var oldMid = i == 0 ? R(0) : mids[i - 1];
            var offset = divisor == 0 ? R(0) : (oldHigh - oldLow) / R(divisor);
            var upper = oldMid + offset; var lower = oldMid - offset; up[i] = upper.ToDouble(); down[i] = lower.ToDouble();
            var start = Math.Max(0L, i - (long)length + 1); var ranges = R(0); var volumes = R(0);
            for (var j = (int)start; j <= i; j++) { ranges += highs[j] - lows[j]; volumes += R(bars[j].Volume); }
            var rangeMean = ranges / R(i - start + 1); var volumeMean = volumes / R(i - start + 1);
            var range = highs[i] - lows[i]; var close = R(b.Close); var volume = R(b.Volume);
            var red = range.CompareTo(rangeMean) > 0 && close.CompareTo(lower) < 0 && volume.CompareTo(volumeMean) > 0 || close.CompareTo(oldMid) < 0;
            var green = close.CompareTo(oldMid) > 0 || range.CompareTo(rangeMean) > 0 && close.CompareTo(upper) > 0 && volume.CompareTo(volumeMean) > 0
                || highs[i].CompareTo(oldHigh) > 0 && range.CompareTo(rangeMean / R(1.5)) < 0 && volume.CompareTo(volumeMean) < 0
                || lows[i].CompareTo(oldLow) < 0 && range.CompareTo(rangeMean / R(1.5)) < 0 && volume.CompareTo(volumeMean) > 0;
            signals[i] = green ? Signal.Buy : red ? Signal.Sell : Signal.None;
        }
        return (new Dictionary<string, double[]> { ["Up"] = up, ["Dn"] = down }, signals);
    }
}

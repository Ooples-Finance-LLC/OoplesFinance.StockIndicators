using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> GroverOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); var cycle = indicator.BatchName == IndicatorName.GroverLlorensCycleOscillator;
        return GroverValues(bars, AverageKind(options, 6), Integer(options, "Length", 100), Integer(options, "SmoothLength", 20), Number(options, cycle ? 10 : 5, "Mult"), cycle).Outputs;
    }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals, double[] Ranges, double[] Oscillators, double[] Gains, double[] Losses) GroverValues(IReadOnlyList<Bar> bars, int kind, int length, int smoothLength, double mult, bool cycle,
        IReadOnlyList<double>? externalAtr = null, IReadOnlyList<double>? externalSmooth = null, IReadOnlyList<double>? externalGains = null, IReadOnlyList<double>? externalLosses = null)
    {
        length = Math.Max(1, length); smoothLength = Math.Max(1, smoothLength);
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        ReferenceFraction Abs(ReferenceFraction value) => value.Sign < 0 ? R(0) - value : value;
        ReferenceFraction[] Average(ReferenceFraction[] values, int period, IReadOnlyList<double>? supplied) => supplied is null ? SmoothRocBankStage(values, period, kind, v => v.RoundExtendedBinary64()) : supplied.Select(R).ToArray();
        var ranges = bars.Select((b, i) => { var previous = R(i == 0 ? b.Close : bars[i - 1].Close); var values = new[] { R(b.High) - R(b.Low), Abs(R(b.High) - previous), Abs(R(b.Low) - previous) }; var value = values[0]; foreach (var next in values.Skip(1)) if (next.CompareTo(value) > 0) value = next; return value.RoundExtendedBinary64(); }).ToArray();
        var atr = Average(ranges, length, externalAtr); var trail = new ReferenceFraction[bars.Count]; var oscillators = new ReferenceFraction[bars.Count]; var signals = new Signal[bars.Count]; var oldDiff = R(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var price = R(bars[i].Close); var previous = i == 0 ? price : trail[i - 1];
            if (!cycle && previous.Sign == 0) previous = i == 0 ? R(0) : R(bars[i - 1].Close);
            var diff = price - previous; trail[i] = (previous - new ReferenceFraction(diff.Sign) * atr[i] * R(mult)).RoundExtendedBinary64();
            oscillators[i] = (price - trail[i]).RoundExtendedBinary64();
            signals[i] = diff.Sign > 0 && diff.CompareTo(oldDiff) > 0 ? Signal.StrongBuy : diff.Sign < 0 && diff.CompareTo(oldDiff) < 0 ? Signal.StrongSell : diff.Sign > 0 ? Signal.Buy : diff.Sign < 0 ? Signal.Sell : Signal.None; oldDiff = diff;
        }
        var line = trail.Select(v => v.ToDouble()).ToArray(); var gains = new ReferenceFraction[bars.Count]; var losses = new ReferenceFraction[bars.Count];
        if (cycle)
        {
            var smooth = Average(oscillators, smoothLength, externalSmooth);
            for (var i = 0; i < bars.Count; i++) { var diff = i == 0 ? R(0) : (smooth[i] - smooth[i - 1]).RoundExtendedBinary64(); gains[i] = diff.Sign > 0 ? diff : R(0); losses[i] = diff.Sign < 0 ? R(0) - diff : R(0); }
            var up = Average(gains, smoothLength, externalGains); var down = Average(losses, smoothLength, externalLosses); var oldLine = R(0); var oldSlope = R(0);
            for (var i = 0; i < bars.Count; i++)
            {
                var total = up[i] + down[i]; line[i] = i > 0 && smoothLength > 1 && kind is 3 or 6 && smooth[i].CompareTo(smooth[i - 1]) == 0 && externalGains is null && externalLosses is null ? line[i - 1]
                    : total.Sign == 0 ? 100 : (new ReferenceFraction(100) * up[i] / total).ToDouble();
                var current = R(line[i]); var slope = current - oldLine;
                signals[i] = slope.Sign > 0 && slope.CompareTo(oldSlope) > 0 ? Signal.StrongBuy : slope.Sign < 0 && slope.CompareTo(oldSlope) < 0 ? Signal.StrongSell
                    : slope.Sign > 0 || oldLine.CompareTo(R(20)) < 0 && current.CompareTo(R(20)) > 0 ? Signal.Buy : slope.Sign < 0 || oldLine.CompareTo(R(80)) > 0 && current.CompareTo(R(80)) < 0 ? Signal.Sell : Signal.None;
                oldLine = current; oldSlope = slope;
            }
        }
        return (new Dictionary<string, double[]> { [cycle ? "Glco" : "Gla"] = line }, signals, ranges.Select(v => v.ToDouble()).ToArray(), oscillators.Select(v => v.ToDouble()).ToArray(), cycle ? gains.Select(v => v.ToDouble()).ToArray() : Array.Empty<double>(), cycle ? losses.Select(v => v.ToDouble()).ToArray() : Array.Empty<double>());
    }
}

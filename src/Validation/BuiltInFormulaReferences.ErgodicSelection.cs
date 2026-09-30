using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> ErgodicSelectionOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var options = indicator.CreateOptions(); return ErgodicSelectionValues(bars, Integer(options, "Length", 32), Integer(options, "SmoothLength", 5), AverageKind(options, 6), Number(options, 1, "PointValue")).Outputs; }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals, double[][] Components) ErgodicSelectionValues(IReadOnlyList<Bar> bars, int length, int smoothLength, int kind, double pointValue = 1, double[][]? external = null)
    {
        length = Math.Max(1, length); smoothLength = Math.Max(1, smoothLength); ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);
        var ranges = new ReferenceFraction[bars.Count]; var up = new ReferenceFraction[bars.Count]; var down = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var old = bars[i == 0 ? i : i - 1]; var h = R(bars[i].High); var l = R(bars[i].Low); var u = h - R(old.High); var d = R(old.Low) - l;
            ranges[i] = new[] { h - l, (h - R(old.Close)).Abs(), (l - R(old.Close)).Abs() }.Max().RoundExtendedBinary64();
            up[i] = u.Sign > 0 && u.CompareTo(d) > 0 ? u.RoundExtendedBinary64() : R(0); down[i] = d.Sign > 0 && d.CompareTo(u) > 0 ? d.RoundExtendedBinary64() : R(0);
        }
        ReferenceFraction[] Mean(ReferenceFraction[] values, int slot) => external is null ? SmoothRocBankStage(values, length, kind) : external[slot].Select(R).ToArray();
        var plus = Mean(up, 0); var minus = Mean(down, 1); var range = Mean(ranges, 2); var dx = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var p = R(range[i].Sign == 0 ? 0 : Math.Max(0, Math.Min(100, (R(100) * plus[i] / range[i]).ToDouble())));
            var m = R(range[i].Sign == 0 ? 0 : Math.Max(0, Math.Min(100, (R(100) * minus[i] / range[i]).ToDouble())));
            dx[i] = R((p + m).Sign == 0 ? 0 : Math.Min(100, ((p - m).Abs() / (p + m)).ToDouble() * 100));
        }
        var adx = Mean(dx, 3); var scale = R(100) * R(pointValue) / (R(Math.Sqrt(length)) * new ReferenceFraction(150L + smoothLength));
        var line = Enumerable.Range(0, bars.Count).Select(i => bars[i].Close <= 0 ? R(0) : (scale * (adx[i] + (i == 0 ? R(0) : adx[i - 1])) / R(2) * ranges[i] / R(length) / R(bars[i].Close)).RoundExtendedBinary64()).ToArray();
        var mean = external is not null && external.Length > 4 ? external[4].Select(R).ToArray() : SmoothRocBankStage(line, smoothLength, kind);
        var signals = new Signal[bars.Count]; var previous = R(0); var previousSlope = R(0);
        for (var i = 0; i < bars.Count; i++) { var slope = mean[i] - previous; signals[i] = slope.Sign > 0 && slope.CompareTo(previousSlope) > 0 ? Signal.StrongBuy : slope.Sign < 0 && slope.CompareTo(previousSlope) < 0 ? Signal.StrongSell : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None; previous = mean[i]; previousSlope = slope; }
        return (new Dictionary<string, double[]> { ["Ecsi"] = line.Select(v => v.ToDouble()).ToArray(), ["Signal"] = mean.Select(v => v.ToDouble()).ToArray() }, signals, new[] { up, down, ranges, dx, line }.Select(v => v.Select(p => p.ToDouble()).ToArray()).ToArray());
    }
}

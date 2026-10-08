using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> CommoditySelectionOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var options = indicator.CreateOptions(); return CommoditySelectionValues(bars, Integer(options, "Length", 14), AverageKind(options, 6)).Outputs; }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals, double[][] Components) CommoditySelectionValues(IReadOnlyList<Bar> bars, int length, int kind, double pointValue = 50, double margin = 3000, double commission = 10, double[][]? external = null)
    {
        length = Math.Max(1, length); ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);
        var ranges = new ReferenceFraction[bars.Count]; var up = new ReferenceFraction[bars.Count]; var down = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var old = bars[i == 0 ? i : i - 1]; var h = R(bars[i].High); var l = R(bars[i].Low); var u = h - R(old.High); var d = R(old.Low) - l;
            ranges[i] = new[] { h - l, (h - R(old.Close)).Abs(), (l - R(old.Close)).Abs() }.Max().RoundExtendedBinary64();
            up[i] = u.Sign > 0 && u.CompareTo(d) > 0 ? u.RoundExtendedBinary64() : R(0); down[i] = d.Sign > 0 && d.CompareTo(u) > 0 ? d.RoundExtendedBinary64() : R(0);
        }
        ReferenceFraction[] Mean(ReferenceFraction[] values, int slot) => external is null ? SmoothRocBankStage(values, length, kind) : external[slot].Select(R).ToArray();
        var atr = Mean(ranges, 0); var plus = Mean(up, 1); var minus = Mean(down, 2); var range = Mean(ranges, 3); var dx = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var p = R(range[i].Sign == 0 ? 0 : Math.Max(0, Math.Min(100, (R(100) * plus[i] / range[i]).ToDouble())));
            var m = R(range[i].Sign == 0 ? 0 : Math.Max(0, Math.Min(100, (R(100) * minus[i] / range[i]).ToDouble())));
            dx[i] = R((p + m).Sign == 0 ? 0 : Math.Min(100, ((p - m).Abs() / (p + m)).ToDouble() * 100));
        }
        var adx = Mean(dx, 4); var scale = R(100) * R(pointValue) / (R(Math.Sqrt(margin)) * (R(150) + R(commission)));
        var line = Enumerable.Range(0, bars.Count).Select(i => (scale * atr[i] * adx[i]).RoundExtendedBinary64()).ToArray();
        var mean = Enumerable.Range(0, bars.Count).Select(i => { var window = Window(line, i, length).ToArray(); return (window.Aggregate(R(0), (a, b) => a + b) / new ReferenceFraction(window.Length)).RoundExtendedBinary64(); }).ToArray();
        var signals = new Signal[bars.Count]; var previous = R(0);
        for (var i = 0; i < bars.Count; i++) { var spread = line[i] - mean[i]; signals[i] = spread.Sign < 0 && spread.CompareTo(previous) < 0 ? Signal.StrongBuy : spread.Sign > 0 && spread.CompareTo(previous) > 0 ? Signal.StrongSell : spread.Sign < 0 ? Signal.Buy : spread.Sign > 0 ? Signal.Sell : Signal.None; previous = spread; }
        return (new Dictionary<string, double[]> { ["Csi"] = line.Select(v => v.ToDouble()).ToArray(), ["Signal"] = mean.Select(v => v.ToDouble()).ToArray() }, signals, new[] { ranges, up, down, ranges, dx }.Select(v => v.Select(p => p.ToDouble()).ToArray()).ToArray());
    }
}

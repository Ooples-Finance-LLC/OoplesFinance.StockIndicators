using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static Dictionary<string, double[]> PhaseChangeOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var options = indicator.CreateOptions(); return PhaseChangeValues(bars, Integer(options, "Length", 35), Integer(options, "SmoothLength", 3), AverageKind(options, 1)).Outputs; }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) PhaseChangeValues(IReadOnlyList<Bar> bars,
        int length, int smooth, int kind = 1, double[]? selected = null, double[]? externalSignal = null)
    {
        length = Math.Max(2, length); smooth = Math.Max(1, smooth);
        ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);
        var zero = R(0); var prices = (selected ?? Closes(bars)).Select(R).ToArray(); var line = new ReferenceFraction[bars.Count];
        for (var i = 0; i < prices.Length; i++)
        {
            var start = i < length ? zero : prices[i - length]; var change = i < length ? zero : prices[i] - start;
            var positive = zero; var total = zero;
            for (var lag = 1; lag <= Math.Min(i, length); lag++)
            {
                var deviation = prices[i - lag] - (start + change * R(lag) / R(length - 1));
                if (deviation.Sign > 0) positive += deviation;
                total += deviation.Sign < 0 ? zero - deviation : deviation;
            }
            line[i] = total.Sign == 0 ? zero : R(100) * positive / total;
        }
        var means = new ReferenceFraction[prices.Length];
        var fallback = externalSignal ?? (kind is not (1 or 2 or 3 or 6) ? Average(line.Select(v => v.ToDouble()).ToArray(), smooth, kind) : null);
        for (var i = 0; i < means.Length; i++)
        {
            if (fallback is not null) { means[i] = R(fallback[i]); continue; }
            var previous = i == 0 ? zero : means[i - 1];
            if (kind == 6) means[i] = (previous * R(smooth - 1) + line[i]) / R(smooth);
            else if (kind == 3 && i >= smooth) means[i] = (previous * R(smooth - 1) + R(2) * line[i]) / R(smooth + 1L);
            else if (kind == 1 && i + 1 < smooth) means[i] = zero;
            else
            {
                var sum = zero;
                for (var j = Math.Max(0, i - smooth + 1); j <= i; j++) sum += line[j] * R(kind == 2 ? smooth - i + j : 1);
                means[i] = sum / new ReferenceFraction(kind == 2 ? (long)smooth * (smooth + 1L) / 2 : Math.Min(i + 1, smooth));
            }
        }
        var trades = new Signal[means.Length];
        for (var i = 0; i < means.Length; i++)
        {
            var prior = i == 0 ? zero : means[i - 1]; var before = i < 2 ? zero : means[i - 2];
            var slope = means[i] - prior; var oldSlope = prior - before;
            trades[i] = slope.Sign > 0 && slope.CompareTo(oldSlope) > 0 ? Signal.StrongBuy
                : slope.Sign < 0 && slope.CompareTo(oldSlope) < 0 ? Signal.StrongSell
                : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None;
        }
        return (new() { ["Pci"] = line.Select(v => v.ToDouble()).ToArray(), ["Signal"] = means.Select(v => v.ToDouble()).ToArray() }, trades);
    }
}

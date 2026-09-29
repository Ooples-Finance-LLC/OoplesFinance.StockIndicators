using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> KaseStopV1Outputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator, bool selected = false)
    { var options = indicator.CreateOptions(); return KaseStopV1Values(bars, Integer(options, "FastLength", 5), Integer(options, "SlowLength", 21), Integer(options, "Length", 20), AverageKind(options, 1), new[] { Number(options, 0, "StdDev1"), Number(options, 1, "StdDev2"), Number(options, 2.2, "StdDev3"), Number(options, 3.6, "StdDev4") }, selected: selected ? bars.Select(b => b.Close).ToArray() : null).Outputs; }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) KaseStopV1Values(IReadOnlyList<Bar> bars, int fastLength, int slowLength, int length, int kind, double[] multiples, double[][]? external = null, double[]? selected = null)
    {
        fastLength = Math.Max(1, fastLength); slowLength = Math.Max(1, slowLength); length = Math.Max(1, length); ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var prices = selected is null ? bars.Select(b => ((R(b.High) + R(b.Low) + R(b.Close)) / R(3)).RoundExtendedBinary64()).ToArray() : selected.Select(R).ToArray();
        ReferenceFraction[] Trend(int period)
        {
            if (kind is 1 or 2) return SmoothRocBankStage(prices, period, kind);
            var result = new ReferenceFraction[prices.Length]; var state = R(0);
            for (var i = 0; i < prices.Length; i++)
            {
                var value = kind == 3 && i < period ? prices.Take(i + 1).Aggregate(R(0), (sum, v) => sum + v) / R(i + 1) : (R(period - 1L) * state + R(kind == 3 ? 2 : 1) * prices[i]) / R(kind == 3 ? period + 1L : period);
                result[i] = value.RoundExtendedBinary64(); state = result[i] + (value - result[i]).RoundExtendedBinary64();
            }
            return result;
        }
        var fast = external is null ? Trend(fastLength) : external[2].Select(R).ToArray(); var slow = external is null ? Trend(slowLength) : external[1].Select(R).ToArray();
        var ranges = bars.Select((b, i) => new[] { R(b.High) - R(i < 2 ? 0 : bars[i - 2].Low), (R(b.High) - R(i < 2 ? 0 : bars[i - 2].Close)).Abs(), (R(b.Low) - R(i < 2 ? 0 : bars[i - 2].Close)).Abs() }.Max().RoundExtendedBinary64()).ToArray();
        var means = external is null ? SmoothRocBankStage(ranges, length, kind) : external[0].Select(R).ToArray(); var outputs = Enumerable.Range(0, 4).Select(_ => new double[bars.Count]).ToArray(); var signals = new Signal[bars.Count]; var before = R(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var deviation = R(0);
            if (i + 1L >= length)
            {
                var values = Window(ranges, i, length).ToArray(); var center = values.Aggregate(R(0), (a, b) => a + b) / R(length); var variance = values.Aggregate(R(0), (sum, v) => sum + (v - center) * (v - center)) / R(length); var scale = R(1);
                while (true) { var root = (variance / scale / scale).SqrtToDouble(); if (!double.IsInfinity(root)) { deviation = R(root) * scale; break; } scale *= R(Math.Pow(2, 512)); }
            }
            var direction = fast[i].CompareTo(slow[i]) < 0 ? 1 : -1;
            for (var j = 0; j < 4; j++) { var stop = (prices[i] + R(direction) * (means[i] + R(multiples[(j + 1) % 4]) * deviation)).RoundExtendedBinary64(); outputs[j][i] = stop.ToDouble(); }
            var difference = fast[i] - slow[i]; signals[i] = difference.Sign > 0 && difference.CompareTo(before) > 0 ? Signal.StrongBuy : difference.Sign < 0 && difference.CompareTo(before) < 0 ? Signal.StrongSell : difference.Sign > 0 ? Signal.Buy : difference.Sign < 0 ? Signal.Sell : Signal.None; before = difference;
        }
        return (Enumerable.Range(0, 4).ToDictionary(i => i == 3 ? "WarningLine" : "Dev" + (i + 1), i => outputs[i]), signals);
    }
}

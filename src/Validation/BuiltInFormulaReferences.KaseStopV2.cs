using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> KaseStopV2Outputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var options = indicator.CreateOptions(); return KaseStopV2Values(bars, Integer(options, "FastLength", 10), Integer(options, "SlowLength", 21), Integer(options, "Length", 20), AverageKind(options, 1), new[] { Number(options, 0, "StdDev1"), Number(options, 1, "StdDev2"), Number(options, 2.2, "StdDev3"), Number(options, 3.6, "StdDev4") }).Outputs; }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) KaseStopV2Values(IReadOnlyList<Bar> bars, int fastLength, int slowLength, int length, int kind, double[] multiples, double[][]? external = null)
    {
        fastLength = Math.Max(1, fastLength); slowLength = Math.Max(1, slowLength); length = Math.Max(1, length); ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var prices = bars.Select(b => R(b.Close)).ToArray(); var fast = external is null ? SmoothRocBankStage(prices, fastLength, kind) : external[0].Select(R).ToArray(); var slow = external is null ? SmoothRocBankStage(prices, slowLength, kind) : external[1].Select(R).ToArray();
        var ranges = bars.Select((b, i) => (new[] { R(b.High), R(i == 0 ? 0 : bars[i - 1].High), R(i < 2 ? 0 : bars[i - 2].Close) }.Max() - new[] { R(b.Low), R(i == 0 ? 0 : bars[i - 1].Low), R(i < 2 ? 0 : bars[i - 2].Close) }.Min()).RoundExtendedBinary64()).ToArray();
        var means = external is null ? SmoothRocBankStage(ranges, length, kind) : external[2].Select(R).ToArray(); var outputs = Enumerable.Range(0, 4).Select(_ => new double[bars.Count]).ToArray(); var signals = new Signal[bars.Count]; var before = R(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var deviation = R(0);
            if (i + 1L >= length)
            {
                var values = Window(ranges, i, length).ToArray(); var center = values.Aggregate(R(0), (a, b) => a + b) / R(length); var variance = values.Aggregate(R(0), (sum, v) => sum + (v - center) * (v - center)) / R(length); var scale = R(1);
                while (true) { var root = (variance / scale / scale).SqrtToDouble(); if (!double.IsInfinity(root)) { deviation = R(root) * scale; break; } scale *= R(Math.Pow(2, 512)); }
            }
            var direction = fast[i].CompareTo(slow[i]) > 0 ? 1 : -1; var price = R(direction > 0 ? bars[i].High : bars[i].Low); var fourth = R(0);
            for (var j = 0; j < 4; j++) { var stop = (price - R(direction) * (means[i] + R(multiples[j]) * deviation)).RoundExtendedBinary64(); outputs[j][i] = stop.ToDouble(); if (j == 3) fourth = stop; }
            var difference = price - fourth; signals[i] = difference.Sign > 0 && difference.CompareTo(before) > 0 ? Signal.StrongBuy : difference.Sign < 0 && difference.CompareTo(before) < 0 ? Signal.StrongSell : difference.Sign > 0 ? Signal.Buy : difference.Sign < 0 ? Signal.Sell : Signal.None; before = difference;
        }
        return (Enumerable.Range(0, 4).ToDictionary(i => "Dev" + (i + 1), i => outputs[i]), signals);
    }
}

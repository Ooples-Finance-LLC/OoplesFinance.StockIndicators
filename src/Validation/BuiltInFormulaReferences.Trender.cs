using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> TrenderOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var options = indicator.CreateOptions(); return TrenderValues(bars, Integer(options, "Length", 14), AverageKind(options, 3), Number(options, 2, "AtrMult")).Outputs; }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) TrenderValues(IReadOnlyList<Bar> bars, int length, int kind, double factor, double[][]? external = null)
    {
        length = Math.Max(1, length); ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value); var prices = bars.Select(b => R(b.Close)).ToArray();
        var mean = external is null ? SmoothRocBankStage(prices, length, kind) : external[0].Select(R).ToArray();
        var ranges = bars.Select((b, i) => { var previous = R(i == 0 ? b.Close : bars[i - 1].Close); return new[] { R(b.High) - R(b.Low), (R(b.High) - previous).Abs(), (R(b.Low) - previous).Abs() }.Max().RoundExtendedBinary64(); }).ToArray();
        var atr = external is null ? SmoothRocBankStage(ranges, length, kind) : external[1].Select(R).ToArray();
        var directions = prices.Select((value, i) => value.CompareTo(i == 0 ? R(0) : prices[i - 1])).ToArray();
        var steps = prices.Select((_, i) => (mean[i] + R(directions[i]) * (atr[i] / R(2)).RoundExtendedBinary64()).RoundExtendedBinary64()).ToArray();
        var adaptive = external is null ? SmoothRocBankStage(steps, length, kind) : external[2].Select(R).ToArray(); var sides = adaptive.Select((value, i) => value.CompareTo(mean[i])).ToArray();
        var up = R(0); var down = R(0); var line = R(0); var before = R(0); var outputs = Enumerable.Range(0, 3).Select(_ => new double[bars.Count]).ToArray(); var signals = new Signal[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var deviation = R(0);
            if (i + 1L >= length)
            {
                var window = Window(atr, i, length).ToArray(); var center = window.Aggregate(R(0), (a, b) => a + b) / R(length); var variance = window.Aggregate(R(0), (sum, value) => sum + (value - center) * (value - center)) / R(length); var scale = R(1);
                while (true) { var root = (variance / scale / scale).SqrtToDouble(); if (!double.IsInfinity(root)) { deviation = R(root) * scale; break; } scale *= R(Math.Pow(2, 512)); }
            }
            if (directions[i] > 0) up = (prices[i] - R(factor) * deviation).RoundExtendedBinary64();
            if (directions[i] < 0) down = (prices[i] + R(factor) * deviation).RoundExtendedBinary64();
            var previousSide = i == 0 ? 0 : sides[i - 1];
            if (sides[i] > 0 && previousSide < 0) up = R(i < 2 ? 0 : bars[i - 2].Low);
            if (sides[i] < 0 && previousSide > 0) down = R(i < 2 ? 0 : bars[i - 2].High);
            if (sides[i] != 0) line = sides[i] > 0 ? up : down;
            outputs[0][i] = up.ToDouble(); outputs[1][i] = down.ToDouble(); outputs[2][i] = line.ToDouble(); var difference = prices[i] - line;
            signals[i] = difference.Sign > 0 && difference.CompareTo(before) > 0 ? Signal.StrongBuy : difference.Sign < 0 && difference.CompareTo(before) < 0 ? Signal.StrongSell : difference.Sign > 0 ? Signal.Buy : difference.Sign < 0 ? Signal.Sell : Signal.None; before = difference;
        }
        return (new Dictionary<string, double[]> { ["TrendUp"] = outputs[0], ["TrendDn"] = outputs[1], ["Trender"] = outputs[2] }, signals);
    }
}

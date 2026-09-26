using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> TrendForceOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); var kind = AverageKind(options, 3);
        var period = Math.Max(1, Math.Min(530, (int)Math.Ceiling(Integer(options, "Length1", 10) / 2d)));
        var anchor = ReferenceFraction.FromDouble(bars.Count == 0 || kind != 3 ? 0 : bars[0].Close);
        var source = bars.Select(b => RoundStrengthStage(ReferenceFraction.FromDouble(b.Close) - anchor)).ToArray();
        var first = SmoothStrengthStage(source, period, kind); var second = SmoothStrengthStage(first, period, kind);
        var zero = new ReferenceFraction(0);
        var force = first.Select((f, i) =>
        {
            var midpoint = (f + second[i]) / new ReferenceFraction(2);
            var previous = i == 0 ? zero : (first[i - 1] + second[i - 1]) / new ReferenceFraction(2);
            var change = midpoint - previous;
            return (f - second[i]).Abs() * change * change * change;
        }).ToArray();
        var lookback = Integer(options, "Length2", 30);
        return Outputs(("Tdfi", force.Select((f, i) =>
        {
            var maximum = Window(force, i, lookback).Select(v => v.Abs()).Max();
            return maximum.Sign == 0 ? 0 : (f / maximum).ToDouble();
        }).ToArray()));
    }
}

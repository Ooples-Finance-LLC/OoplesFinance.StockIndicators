using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> VortexOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var length = Integer(indicator.CreateOptions(), "Length", 14); var zero = new ReferenceFraction(0);
        var highs = bars.Select(b => ReferenceFraction.FromDouble(b.High)).ToArray();
        var lows = bars.Select(b => ReferenceFraction.FromDouble(b.Low)).ToArray();
        var closes = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        var ranges = bars.Select((_, i) =>
        {
            var previous = i == 0 ? closes[i] : closes[i - 1];
            return new[] { highs[i] - lows[i], (highs[i] - previous).Abs(), (lows[i] - previous).Abs() }.Max();
        }).ToArray();
        var plus = bars.Select((_, i) => i == 0 ? zero : (highs[i] - lows[i - 1]).Abs()).ToArray();
        var minus = bars.Select((_, i) => i == 0 ? zero : (lows[i] - highs[i - 1]).Abs()).ToArray();
        double[] Ratio(ReferenceFraction[] changes) => changes.Select((_, i) =>
        {
            var denominator = Window(ranges, i, length).Aggregate(zero, (a, b) => a + b);
            return denominator.Sign == 0 ? 0 : (Window(changes, i, length).Aggregate(zero, (a, b) => a + b) / denominator).ToDouble();
        }).ToArray();
        return Outputs(("ViPlus", Ratio(plus)), ("ViMinus", Ratio(minus)));
    }
}

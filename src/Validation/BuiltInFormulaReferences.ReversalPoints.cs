using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> ReversalPointsOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); var length = Integer(options, "Length", 100);
        var smooth = Math.Max(2, Math.Min(530, (int)Math.Ceiling(length / 2d)));
        var movement = bars.Select((b, i) => RoundStrengthStage((ReferenceFraction.FromDouble(b.Close) -
            ReferenceFraction.FromDouble(i == 0 ? 0 : bars[i - 1].Close)).Abs())).ToArray();
        var first = SmoothStrengthStage(movement, smooth, AverageKind(options, 3));
        var second = SmoothStrengthStage(first, smooth, AverageKind(options, 3));
        var ratios = first.Select((v, i) => second[i].Sign == 0 ? 0 : (v / second[i]).ToDouble()).ToArray();
        return Outputs(("Rp", ratios.Select((_, i) => Window(ratios, i, length)
            .Aggregate(new ReferenceFraction(0), (sum, value) => sum + ReferenceFraction.FromDouble(value)).ToDouble()).ToArray()));
    }
}

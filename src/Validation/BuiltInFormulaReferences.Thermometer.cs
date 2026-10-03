using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> ThermometerOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var zero = new ReferenceFraction(0);
        var expansion = bars.Select((b, i) =>
        {
            var up = ReferenceFraction.FromDouble(b.High) - ReferenceFraction.FromDouble(i == 0 ? 0 : bars[i - 1].High);
            var down = ReferenceFraction.FromDouble(i == 0 ? 0 : bars[i - 1].Low) - ReferenceFraction.FromDouble(b.Low);
            return RoundStrengthStage(new[] { zero, up, down }.Max());
        }).ToArray();
        var options = indicator.CreateOptions();
        var smooth = SmoothStrengthStage(expansion, Integer(options, "Length", 22), AverageKind(options, 3));
        return Outputs(("Emt", expansion.Select(v => v.ToDouble()).ToArray()), ("Signal", smooth.Select(v => v.ToDouble()).ToArray()));
    }
}

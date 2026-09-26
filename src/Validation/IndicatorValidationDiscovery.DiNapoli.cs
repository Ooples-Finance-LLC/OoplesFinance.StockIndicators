using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

public static partial class IndicatorValidationDiscovery
{
    private static IEnumerable<IndicatorValidationCase> DiNapoliPeriodCases(Type type)
    {
        if (type != typeof(DiNapoliMovingAverageConvergenceDivergence)) yield break;
        foreach (var (name, slow, fast, signal) in new[]
        {
            ("minimum", 1d, 1d, 1d),
            ("short", 3d, 1d, 2d),
            ("fractional-signal", 17.5185, 8.3896, 1.5),
            ("reversed", 8.3896, 17.5185, 9.0503),
            ("sub-unit", .5, 1.5, 2d),
            ("maximum", double.MaxValue, 1d, 3d)
        })
            yield return new IndicatorValidationCase(type, "fractional-periods/" + name,
                () => new DiNapoliMovingAverageConvergenceDivergence(slow, fast, signal));
    }
}

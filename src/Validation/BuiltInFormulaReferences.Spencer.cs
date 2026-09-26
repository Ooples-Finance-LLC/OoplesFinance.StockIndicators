using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> SpencerOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var longer = indicator.BatchName == IndicatorName.Spencer21PointMovingAverage;
        var coefficients = longer ? new[] { -1, -3, -5, -5, -2, 6, 18, 33, 47, 57, 60, 57, 47, 33, 18, 6, -2, -5, -5, -3, -1 }
            : new[] { -3, -6, -5, 3, 21, 46, 67, 74, 67, 46, 21, 3, -5, -6, -3 };
        var denominator = new ReferenceFraction(coefficients.Sum());
        var values = bars.Select((_, i) => Enumerable.Range(0, Math.Min(i + 1, coefficients.Length))
            .Aggregate(new ReferenceFraction(0), (sum, lag) => sum + new ReferenceFraction(coefficients[lag]) * ReferenceFraction.FromDouble(bars[i - lag].Close))
            / denominator).Select(value => value.ToDouble()).ToArray();
        return Outputs((longer ? "S21ma" : "S15ma", values));
    }
}

using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoundedSineMean(IReadOnlyList<Bar> bars, int length)
    {
        var coefficients = Enumerable.Range(1, length)
            .Select(j => ReferenceFraction.FromDouble(Math.Sin(j * Math.PI / (length + 1d)))).ToArray();
        var total = coefficients.Aggregate(new ReferenceFraction(0), (sum, c) => sum + c);
        return bars.Select((_, i) => (Enumerable.Range(0, Math.Min(length, i + 1))
            .Aggregate(new ReferenceFraction(0), (sum, lag) => sum + coefficients[lag] * ReferenceFraction.FromDouble(bars[i - lag].Close))
            / total).ToDouble()).ToArray();
    }
}

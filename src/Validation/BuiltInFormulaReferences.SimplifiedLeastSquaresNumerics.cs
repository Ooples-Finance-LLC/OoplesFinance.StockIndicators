using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoundedSimplifiedLeastSquares(IReadOnlyList<Bar> bars, int length)
    {
        var denominator = new ReferenceFraction((long)length * (length + 1L));
        return bars.Select((_, i) =>
        {
            var numerator = new ReferenceFraction(0);
            for (var lag = 0; lag < Math.Min(length, i + 1); lag++)
                numerator += ReferenceFraction.FromDouble(bars[i - lag].Close)
                    * new ReferenceFraction(6L * (length - lag) - 2L * (length + 1L));
            return (numerator / denominator).ToDouble();
        }).ToArray();
    }
}

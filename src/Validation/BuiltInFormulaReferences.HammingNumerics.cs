using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoundedHammingMean(IReadOnlyList<Bar> bars, int length, double pedestal = 3)
    {
        // Independent rational convolution of the documented binary64 sine taps.
        var weights = Enumerable.Range(0, length).Select(j =>
        {
            var mirror = j < (length + 1L) / 2 ? j : length - 1 - j;
            var fraction = length == 1 ? 0 : mirror / (length - 1d);
            return ReferenceFraction.FromDouble(length == 1 ? 1
                : Math.Sin(pedestal * (1 - 2 * fraction) + Math.PI * fraction));
        }).ToArray();
        var denominator = weights.Aggregate(new ReferenceFraction(0), (sum, w) => sum + w);
        if (denominator.Sign == 0) return new double[bars.Count];
        return bars.Select((_, i) => (Enumerable.Range(0, Math.Min(length, i + 1))
            .Aggregate(new ReferenceFraction(0), (sum, lag) => sum + weights[lag]
                * ReferenceFraction.FromDouble(bars[i - lag].Close)) / denominator).ToDouble()).ToArray();
    }
}

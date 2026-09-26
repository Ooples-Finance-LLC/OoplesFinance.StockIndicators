using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoundedAlmaMean(IReadOnlyList<Bar> bars, int length, double offset = .85, double sigma = 6)
    {
        // The Gaussian coefficient contract uses binary64 normalized locations.
        // Independent rational convolution retains every product bit until the
        // final division; missing history is zero, with the full denominator.
        var weights = Enumerable.Range(0, length).Select(j =>
        {
            var location = offset == .5 // NOSONAR: S1244 - Only the exact midpoint selects the symmetric Gaussian contract.
                ? (j - (length - 1d) / 2) / length * sigma
                : (j / (double)length - offset * ((length - 1d) / length)) * sigma;
            return ReferenceFraction.FromDouble(Math.Exp(-0.5 * location * location));
        }).ToArray();
        var denominator = weights.Aggregate(new ReferenceFraction(0), (s, w) => s + w);
        if (denominator.Sign == 0) return new double[bars.Count];
        return bars.Select((_, i) => (Enumerable.Range(0, length)
            .Where(j => i - length + 1 + j >= 0)
            .Aggregate(new ReferenceFraction(0), (sum, j) => sum + weights[j]
                * ReferenceFraction.FromDouble(bars[i - length + 1 + j].Close)) / denominator).ToDouble()).ToArray();
    }
}

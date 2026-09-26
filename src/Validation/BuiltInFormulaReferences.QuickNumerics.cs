using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoundedQuickMean(IReadOnlyList<Bar> bars, int length)
    {
        var peak = Math.Max(2, Math.Min(530, (int)Math.Ceiling(length / 3d)));
        // Reference the original piecewise fractional taps. Production cancels
        // their common denominator and uses a closed-form integer weight sum.
        var weights = new ReferenceFraction[length + 1];
        var denominator = new ReferenceFraction(0);
        for (var lag = 0; lag < weights.Length; lag++)
        {
            weights[lag] = lag + 1 <= peak ? new ReferenceFraction(lag + 1) / new ReferenceFraction(peak)
                : new ReferenceFraction(length - lag) / new ReferenceFraction(length + 1L - peak);
            denominator += weights[lag];
        }
        var result = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var sum = new ReferenceFraction(0);
            for (var lag = 0; lag < weights.Length && lag <= i; lag++)
                sum += ReferenceFraction.FromDouble(bars[i - lag].Close) * weights[lag];
            result[i] = (sum / denominator).ToDouble();
        }
        return result;
    }
}

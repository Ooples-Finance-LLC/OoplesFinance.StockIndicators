using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoundedFibonacciMean(IReadOnlyList<Bar> bars, int length)
    {
        // Forward rational recurrence and explicit total are independent of the
        // production doubling identities, descending recurrence and sum identity.
        var weights = new ReferenceFraction[length];
        var previous = new ReferenceFraction(0);
        var current = new ReferenceFraction(1);
        var denominator = new ReferenceFraction(0);
        for (var i = 0; i < length; i++)
        {
            weights[i] = current;
            denominator += current;
            (previous, current) = (current, previous + current);
        }
        var result = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var numerator = new ReferenceFraction(0);
            for (var lag = 0; lag < length && lag <= i; lag++)
                numerator += ReferenceFraction.FromDouble(bars[i - lag].Close) * weights[length - lag - 1];
            result[i] = (numerator / denominator).ToDouble();
        }
        return result;
    }
}

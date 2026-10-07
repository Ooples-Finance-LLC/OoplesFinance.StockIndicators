using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoundedPowerMean(IReadOnlyList<Bar> bars, int length, int power)
    {
        // Explicit rational weights provide an independent check of the production
        // closed-form denominator and exact integer accumulation.
        ReferenceFraction Weight(int distance)
        {
            var weight = new ReferenceFraction(1);
            for (var p = 0; p < power; p++) weight *= new ReferenceFraction(distance);
            return weight;
        }
        var denominator = new ReferenceFraction(0);
        for (var lag = 0; lag < length; lag++) denominator += Weight(lag + 1);
        var result = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var numerator = new ReferenceFraction(0);
            for (var lag = 0; lag < length && lag <= i; lag++)
                numerator += ReferenceFraction.FromDouble(bars[i - lag].Close) * Weight(length - lag);
            result[i] = (numerator / denominator).ToDouble();
        }
        return result;
    }
}

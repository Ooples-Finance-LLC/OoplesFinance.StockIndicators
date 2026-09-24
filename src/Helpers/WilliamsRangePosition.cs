namespace OoplesFinance.StockIndicators.Helpers;

internal static class WilliamsRangePosition
{
    // Unclamped Williams %R. Infinity is returned only when the final correctly rounded
    // result is outside binary64 range; the typed runtime rejects that output.
    internal static double Percent(double value, double lower, double upper)
    {
        if (upper == lower) return -100; // NOSONAR: S1244 - Equal bounds define an exactly zero range; a nonzero range must still be evaluated.
        if (value == upper) return upper > lower ? -0d : 0d; // NOSONAR: S1244 - Exact contact with the upper bound preserves the defined signed zero.
        var numerator = new ExactMeanAccumulator();
        numerator.Add(value, 100);
        numerator.Add(upper, -100);
        var denominator = new ExactMeanAccumulator();
        denominator.Add(upper);
        denominator.Add(lower, -1);
        return numerator.Ratio(denominator);
    }
}

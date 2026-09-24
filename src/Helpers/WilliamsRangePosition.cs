namespace OoplesFinance.StockIndicators.Helpers;

internal static class WilliamsRangePosition
{
    // Unclamped Williams %R. Infinity is returned only when the final correctly rounded
    // result is outside binary64 range; the typed runtime rejects that output.
    internal static double Percent(double value, double lower, double upper)
    {
        if (upper == lower) return -100;
        if (value == upper) return upper > lower ? -0d : 0d;
        var numerator = new ExactMeanAccumulator();
        numerator.Add(value, 100);
        numerator.Add(upper, -100);
        var denominator = new ExactMeanAccumulator();
        denominator.Add(upper);
        denominator.Add(lower, -1);
        return numerator.Ratio(denominator);
    }
}

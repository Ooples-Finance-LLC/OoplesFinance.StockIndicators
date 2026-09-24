namespace OoplesFinance.StockIndicators.Helpers;

internal static class ClampedRangePosition
{
    // Preserve the defined orientation even for reversed supplied endpoints.
    // Clamp before division so an out-of-range ratio need never be representable.
    internal static double Percent(double value, double lower, double upper)
    {
        if (upper == lower) return 0;
        if (upper > lower)
        {
            if (value <= lower) return 0;
            if (value >= upper) return 100;
        }
        else
        {
            if (value >= lower) return 0;
            if (value <= upper) return 100;
        }
        var numerator = new ExactMeanAccumulator();
        numerator.Add(value, 100);
        numerator.Add(lower, -100);
        var denominator = new ExactMeanAccumulator();
        denominator.Add(upper);
        denominator.Add(lower, -1);
        return numerator.Ratio(denominator);
    }
}

namespace OoplesFinance.StockIndicators.Helpers;

internal static class ExactRangePosition
{
    // Preserve the unconstrained ratio, including selected prices outside the range.
    internal static double Fraction(double value, double lower, double upper)
    {
        if (upper == lower) return 0;
        var numerator = new ExactMeanAccumulator();
        numerator.Add(value);
        numerator.Add(lower, -1);
        var denominator = new ExactMeanAccumulator();
        denominator.Add(upper);
        denominator.Add(lower, -1);
        return numerator.Ratio(denominator);
    }
}

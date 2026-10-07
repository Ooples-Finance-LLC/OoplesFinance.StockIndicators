namespace OoplesFinance.StockIndicators.Helpers;

internal static class ExactRangePosition
{
    // Preserve the unconstrained ratio, including selected prices outside the range.
    internal static double Fraction(double value, double lower, double upper)
    {
#pragma warning disable S1244 // Only an exactly zero range uses the degenerate result; nonzero subnormal ranges remain meaningful.
        if (upper == lower) return 0;
#pragma warning restore S1244
        var numerator = new ExactMeanAccumulator();
        numerator.Add(value);
        numerator.Add(lower, -1);
        var denominator = new ExactMeanAccumulator();
        denominator.Add(upper);
        denominator.Add(lower, -1);
        return numerator.Ratio(denominator);
    }
}

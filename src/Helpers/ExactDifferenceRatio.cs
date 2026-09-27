namespace OoplesFinance.StockIndicators.Helpers;

internal static class ExactDifferenceRatio
{
    internal static double Of(double left, double right, double upper, double lower)
    {
#pragma warning disable S1244 // Only an exactly zero range uses the degenerate result; nonzero subnormal ranges remain meaningful.
        if (upper == lower) return 0;
#pragma warning restore S1244
        var numerator = new ExactMeanAccumulator();
        numerator.Add(left);
        numerator.Add(right, -1);
        var denominator = new ExactMeanAccumulator();
        denominator.Add(upper);
        denominator.Add(lower, -1);
        return numerator.Ratio(denominator);
    }
}

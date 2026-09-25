namespace OoplesFinance.StockIndicators.Helpers;

internal static class ExactDifferenceRatio
{
    internal static double Of(double left, double right, double upper, double lower)
    {
        if (upper == lower) return 0;
        var numerator = new ExactMeanAccumulator();
        numerator.Add(left);
        numerator.Add(right, -1);
        var denominator = new ExactMeanAccumulator();
        denominator.Add(upper);
        denominator.Add(lower, -1);
        return numerator.Ratio(denominator);
    }
}

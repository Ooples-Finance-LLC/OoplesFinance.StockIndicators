namespace OoplesFinance.StockIndicators.Helpers;

internal static class RoundedRangeRatio
{
    internal static double Of(double high, double low, double divisor)
    {
        if (divisor == 0) return 0;
        var numerator = new ExactMeanAccumulator();
        numerator.Add(high);
        numerator.Add(low, -1);
        var denominator = new ExactMeanAccumulator();
        denominator.Add(divisor);
        return numerator.Ratio(denominator);
    }
}

namespace OoplesFinance.StockIndicators.Helpers;

internal static class RoundedStochasticMacd
{
    // The shared low cancels: 10*((fast-low)/(high-low)-(slow-low)/(high-low)).
    internal static double Of(double fast, double slow, double high, double low)
    {
        if (high == low) return 0;
        var numerator = new ExactMeanAccumulator();
        numerator.Add(fast, 10);
        numerator.Add(slow, -10);
        var denominator = new ExactMeanAccumulator();
        denominator.Add(high);
        denominator.Add(low, -1);
        return numerator.Ratio(denominator);
    }
}

namespace OoplesFinance.StockIndicators.Helpers;

internal static class RoundedStochasticMacd
{
    // The shared low cancels: 10*((fast-low)/(high-low)-(slow-low)/(high-low)).
    internal static double Of(double fast, double slow, double high, double low)
    {
#pragma warning disable S1244 // Only an exactly zero range uses the degenerate result; nonzero subnormal ranges remain meaningful.
        if (high == low) return 0;
#pragma warning restore S1244
        var numerator = new ExactMeanAccumulator();
        numerator.Add(fast, 10);
        numerator.Add(slow, -10);
        var denominator = new ExactMeanAccumulator();
        denominator.Add(high);
        denominator.Add(low, -1);
        return numerator.Ratio(denominator);
    }
}

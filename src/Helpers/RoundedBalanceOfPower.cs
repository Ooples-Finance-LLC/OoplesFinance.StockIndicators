namespace OoplesFinance.StockIndicators.Helpers;

internal static class RoundedBalanceOfPower
{
    internal static double Of(double open, double high, double low, double close)
    {
        if (high == low) return 0; // NOSONAR: S1244 - Equal bounds define an exactly zero range; a nonzero range must still be evaluated.
        var numerator = new ExactMeanAccumulator();
        numerator.Add(close);
        numerator.Add(open, -1);
        var denominator = new ExactMeanAccumulator();
        denominator.Add(high);
        denominator.Add(low, -1);
        return numerator.Ratio(denominator);
    }
}

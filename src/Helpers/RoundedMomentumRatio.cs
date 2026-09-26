namespace OoplesFinance.StockIndicators.Helpers;

internal static class RoundedMomentumRatio
{
    // Round the final percentage ratio once, including when division alone underflows.
    internal static double Of(double current, double previous)
    {
        if (previous == 0) return 0;
        var numerator = new ExactMeanAccumulator();
        numerator.Add(current, 100);
        var denominator = new ExactMeanAccumulator();
        denominator.Add(previous);
        return numerator.Ratio(denominator);
    }

}

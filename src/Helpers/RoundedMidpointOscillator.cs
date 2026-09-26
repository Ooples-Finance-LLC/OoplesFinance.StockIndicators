namespace OoplesFinance.StockIndicators.Helpers;

internal static class RoundedMidpointOscillator
{
    internal static double Of(double value, double high, double low)
    {
        if (high == low) return 0;
        var numerator = new ExactMeanAccumulator();
        numerator.Add(value, 200); numerator.Add(high, -100); numerator.Add(low, -100);
        var denominator = new ExactMeanAccumulator();
        denominator.Add(high); denominator.Add(low, -1);
        return Math.Max(-100, Math.Min(100, numerator.Ratio(denominator)));
    }
}

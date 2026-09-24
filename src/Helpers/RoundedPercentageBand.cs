namespace OoplesFinance.StockIndicators.Helpers;

internal static class RoundedPercentageBand
{
    internal static double Of(double center, double fraction, int direction)
    {
        if (double.IsNaN(center) || double.IsInfinity(center)) return center;
        var value = new ExactMeanAccumulator();
        value.Add(center);
        value.AddProduct(center, fraction, direction);
        return value.Mean(1);
    }
}

namespace OoplesFinance.StockIndicators.Helpers;

internal static class RoundedPercentageBand
{
    internal static double Percent(double center, double percent, int direction)
    {
        if (double.IsNaN(center) || double.IsInfinity(center)) return center;
        var value = new ExactMeanAccumulator();
        value.Add(center, 100);
        value.AddProduct(center, percent, direction);
        return value.Mean(100);
    }

    internal static double Of(double center, double fraction, int direction)
    {
        if (double.IsNaN(center) || double.IsInfinity(center)) return center;
        var value = new ExactMeanAccumulator();
        value.Add(center);
        value.AddProduct(center, fraction, direction);
        return value.Mean(1);
    }
}

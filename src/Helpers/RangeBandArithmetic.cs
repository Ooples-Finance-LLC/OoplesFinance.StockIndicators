namespace OoplesFinance.StockIndicators.Helpers;

internal static class RangeBandArithmetic
{
    internal static double Midpoint(double first, double second)
    {
        var sum = new ExactMeanAccumulator(); sum.Add(first); sum.Add(second);
        return sum.Mean(2);
    }

    internal static double Band(double middle, double high, double low, double multiplier)
    {
        var sum = new ExactMeanAccumulator();
        sum.Add(middle); sum.AddProduct(high, multiplier); sum.AddProduct(low, multiplier, -1);
        return sum.Mean(1);
    }
}

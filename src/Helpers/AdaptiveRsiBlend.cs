namespace OoplesFinance.StockIndicators.Helpers;

internal static class AdaptiveRsiBlend
{
    internal static double Next(double price, double previous, double rsi)
    {
        var alpha = 2 * Math.Abs(rsi / 100 - 0.5);
        var sum = new ExactMeanAccumulator();
        sum.Add(previous);
        sum.AddProduct(price, alpha);
        sum.AddProduct(previous, alpha, -1);
        return sum.Mean(1);
    }
}

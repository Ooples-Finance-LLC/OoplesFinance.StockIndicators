namespace OoplesFinance.StockIndicators.Helpers;

internal static class RoundedWilder
{
    internal static double Next(double value, double previous, int length)
    {
        var sum = new ExactMeanAccumulator();
        sum.Add(value);
        sum.Add(previous, length - 1);
        return sum.Mean(length);
    }
}

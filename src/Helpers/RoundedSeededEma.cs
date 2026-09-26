namespace OoplesFinance.StockIndicators.Helpers;

internal static class RoundedSeededEma
{
    // The caller seeds the first sample; each following convex combination rounds once.
    internal static double Next(double value, double previous, int length)
    {
        length = Math.Max(1, length);
        var sum = new ExactMeanAccumulator();
        sum.Add(value, 2);
        sum.Add(previous, length - 1);
        return sum.Mean((long)length + 1);
    }
}

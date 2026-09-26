namespace OoplesFinance.StockIndicators.Helpers;

internal static class ConnorsValue
{
    internal static double Combine(double priceRsi, double rank, double streakRsi)
    {
        var sum = new ExactMeanAccumulator();
        sum.Add(priceRsi); sum.Add(rank); sum.Add(streakRsi);
        return Math.Max(0, Math.Min(100, sum.Mean(3)));
    }
}

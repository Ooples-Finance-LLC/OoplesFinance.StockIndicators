namespace OoplesFinance.StockIndicators.Helpers;

internal static class ExponentialExtrapolation
{
    internal static double Double(double first, double second)
    {
        var sum = new ExactMeanAccumulator();
        sum.Add(first, 2);
        sum.Add(second, -1);
        return sum.Mean(1);
    }

    internal static double Triple(double first, double second, double third)
    {
        var sum = new ExactMeanAccumulator();
        sum.Add(first, 3);
        sum.Add(second, -3);
        sum.Add(third);
        return sum.Mean(1);
    }
}

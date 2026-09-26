namespace OoplesFinance.StockIndicators.Helpers;

internal static class LeoAverage
{
    // The component averages are already rounded. Combine them exactly before
    // rounding the exposed result, even when twice the WMA is unrepresentable.
    internal static double Combine(double weighted, double simple)
    {
        var sum = new ExactMeanAccumulator();
        sum.Add(weighted, 2);
        sum.Add(simple, -1);
        return sum.Mean(1);
    }
}

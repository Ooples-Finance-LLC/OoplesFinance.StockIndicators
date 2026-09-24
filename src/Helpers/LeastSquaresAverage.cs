namespace OoplesFinance.StockIndicators.Helpers;

internal static class LeastSquaresAverage
{
    // Round only after combining the two already-rounded component averages.
    internal static double Combine(double weighted, double simple)
    {
        var sum = new ExactMeanAccumulator();
        sum.Add(weighted, 3);
        sum.Add(simple, -2);
        return sum.Mean(1);
    }
}

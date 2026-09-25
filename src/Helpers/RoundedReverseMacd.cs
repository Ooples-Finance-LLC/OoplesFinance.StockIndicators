namespace OoplesFinance.StockIndicators.Helpers;

internal static class RoundedReverseMacd
{
    // Solve a*(price-fast) = b*(price-slow), using the published rounded gains.
    internal static double Equilibrium(double fast, double slow, double a, double b)
    {
        if (a == b) return fast;
        if (double.IsNaN(fast) || double.IsInfinity(fast) || double.IsNaN(slow) || double.IsInfinity(slow)) return double.NaN;
        var numerator = new ExactMeanAccumulator();
        numerator.AddProduct(fast, a);
        numerator.AddProduct(slow, b, -1);
        var denominator = new ExactMeanAccumulator();
        denominator.Add(a);
        denominator.Add(b, -1);
        return numerator.Ratio(denominator);
    }
}

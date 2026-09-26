namespace OoplesFinance.StockIndicators.Helpers;

// The gain is a rounded binary64 coefficient in [0,1]. Accumulate the
// convex blend exactly without rounding 1-gain or subtracting prices first.
internal static class VidyaBlend
{
    internal static double Compute(double previous, double current, double gain)
    {
        var sum = new ExactMeanAccumulator();
        sum.Add(previous);
        sum.AddProduct(previous, gain, -1);
        sum.AddProduct(current, gain);
        return sum.Mean(1);
    }
}

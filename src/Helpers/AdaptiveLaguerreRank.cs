namespace OoplesFinance.StockIndicators.Helpers;

/// <summary>Ranks deviations without treating subtraction roundoff as new adaptive information.</summary>
internal static class AdaptiveLaguerreRank
{
    internal static double Calculate(double deviation, double low, double high, double value, double previousFilter)
    {
        // A deviation subtracts two prices produced by the four-stage lattice. Resolve ties
        // within 32 machine epsilons of their price scale before dividing by the window range.
        var uncertainty = 32 * 2.2204460492503131e-16 * Math.Max(Math.Abs(value), Math.Abs(previousFilter));
        var range = high - low;
        if (range <= uncertainty || deviation - low <= uncertainty) return 0;
        if (high - deviation <= uncertainty) return 1;
        return (deviation - low) / range;
    }
}

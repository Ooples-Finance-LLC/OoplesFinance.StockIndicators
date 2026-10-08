namespace OoplesFinance.StockIndicators.Helpers;

internal static class SchaffRange
{
    // Subtracting two price averages introduces roundoff on the scale of those averages.
    // A range below 64 machine epsilons of that scale is numerically flat; normalizing it
    // would turn insignificant cancellation noise into a full-scale stochastic reading.
    internal static double Normalize(double value, double lower, double upper, double averageScale, double flatValue = 0)
    {
        var range = upper - lower;
        return range <= 1.4210854715202004e-14 * averageScale ? flatValue
            : Math.Max(0, Math.Min(100, 100 * (value - lower) / range));
    }
}

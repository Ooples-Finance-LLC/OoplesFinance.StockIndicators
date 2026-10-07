namespace OoplesFinance.StockIndicators.Helpers;

internal static class DynamicMomentumPeriod
{
    internal static int Calculate(double deviation, double averageDeviation, int baseline, int minimum, int maximum)
    {
        minimum = Math.Max(1, minimum);
        maximum = Math.Max(minimum, maximum);
        // Volatility is relative to its own average, so a change of price units cannot change the period.
        var period = averageDeviation <= 0 ? Math.Max(1, baseline)
            : deviation <= 0 ? maximum : Math.Max(1, baseline) * (averageDeviation / deviation);
        period = Math.Max(minimum, Math.Min(maximum, period));
        // Treat roundoff-sized distances below an integer as that integer before truncating the period.
        var nearest = Math.Round(period);
        if (Math.Abs(period - nearest) <= 1e-12 * Math.Max(1, period)) period = nearest;
        return (int)Math.Floor(period);
    }
}

namespace OoplesFinance.StockIndicators.Helpers;

internal static class DynamicMomentumPeriod
{
    // The snapshot has already rounded its positive finite volatility ratio.
    // Preserve exact floor(baseline / ratio), unlike Calculate's tolerance rule.
    internal static int SelectExact(double ratio, int baseline, int minimum, int maximum)
    {
        var numerator = JurikDyadic.FromDouble(baseline);
        int CompareProduct(int period)
        {
            var product = JurikDyadic.FromDouble(ratio);
            product.Multiply(period);
            product.Subtract(numerator);
            return product.Sign;
        }
        if (CompareProduct(maximum) <= 0) return maximum;
        if (CompareProduct(minimum) >= 0) return minimum;
        // Exact quotient lies strictly inside positive Int32 bounds. Division
        // can round up across an integer, but by less than one. An exact
        // integer quotient is representable, so it cannot round below itself.
        int candidate = (int)(baseline / ratio);
        return CompareProduct(candidate) > 0 ? candidate - 1 : candidate;
    }

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

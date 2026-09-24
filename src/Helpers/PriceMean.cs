namespace OoplesFinance.StockIndicators.Helpers;

/// <summary>Small price means with a guarded exact-sum fallback for finite inputs.</summary>
internal static class PriceMean
{
    internal static double Of(double a, double b) => Mean(a, b, 0, 0, 2);
    internal static double Of(double a, double b, double c) => Mean(a, b, c, 0, 3);
    internal static double Of(double a, double b, double c, double d) => Mean(a, b, c, d, 4);

    private static double Mean(double a, double b, double c, double d, int count)
    {
        var ab = a + b;
        var abc = ab + c;
        var sum = abc + d;
        var magnitude = Math.Max(Math.Max(Math.Abs(a), Math.Abs(b)), Math.Max(Math.Abs(c), Math.Abs(d)));
        // With at most four inputs, bounding the total condition number avoids cascaded
        // cancellations missed by pairwise heuristics. Tiny values use exact rounding too.
        if (!double.IsNaN(sum) && !double.IsInfinity(sum)
            && (magnitude == 0 || magnitude >= 1e-300 && Math.Abs(sum) > 1e-4 * magnitude)) return sum / count;
        var exact = new ExactMeanAccumulator();
        exact.Add(a); exact.Add(b); exact.Add(c); exact.Add(d);
        return exact.Mean(count);
    }
}

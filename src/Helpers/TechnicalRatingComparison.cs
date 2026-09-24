namespace OoplesFinance.StockIndicators.Helpers;

// Voting is discrete: numerically equal component values must cast a neutral vote.
internal static class TechnicalRatingComparison
{
    internal static int Compare(double left, double right)
    {
        var delta = left - right;
        var resolution = 1e-12 * Math.Max(1, Math.Max(Math.Abs(left), Math.Abs(right)));
        return Math.Abs(delta) <= resolution ? 0 : delta > 0 ? 1 : -1;
    }
}

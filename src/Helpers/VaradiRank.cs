namespace OoplesFinance.StockIndicators.Helpers;

internal static class VaradiRank
{
    // Ratios separated only by accumulated smoothing roundoff represent a tie.
    // This resolution is part of the rank contract, not a tolerance on the output percentage.
    internal static double InclusiveBoundary(double value) => value + 1e-12 * Math.Max(1, Math.Abs(value));
}

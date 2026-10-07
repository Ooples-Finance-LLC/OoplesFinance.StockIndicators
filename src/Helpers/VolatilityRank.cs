namespace OoplesFinance.StockIndicators.Helpers;

internal static class VolatilityRank
{
    // Values within 32 binary64 relative roundoff units are ties. Without this boundary,
    // reordering equivalent residual sums can move an entire percentile rank.
    internal static double StrictBoundary(double value) => value - Math.Abs(value) * (32 * 2.2204460492503131e-16);
}

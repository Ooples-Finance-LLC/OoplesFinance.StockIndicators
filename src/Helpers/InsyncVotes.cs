namespace OoplesFinance.StockIndicators.Helpers;

// Discrete votes must not turn rounding residuals from equivalent component calculations into +/-5.
// Values within 1e-12 relative to max(1, magnitude) are a tie; the original equality rules then apply.
internal static class InsyncVotes
{
    private static int Compare(double value, double boundary)
    {
        var difference = value - boundary;
        var resolution = 1e-12 * Math.Max(1, Math.Max(Math.Abs(value), Math.Abs(boundary)));
        return Math.Abs(difference) <= resolution ? 0 : difference > 0 ? 1 : -1;
    }
    internal static double Band(double value, double lower, double upper) =>
        Compare(value, lower) < 0 ? -5 : Compare(value, upper) > 0 ? 5 : 0;
    internal static double Direction(double value, double mean) => Compare(value, mean) < 0
        ? Compare(mean, 0) < 0 ? -5 : 0 : Compare(mean, 0) > 0 ? 5 : 0;
    internal static double InverseDirection(double value, double mean) => Compare(value, mean) > 0
        ? Compare(mean, 0) > 0 ? 5 : 0 : Compare(mean, 0) < 0 ? -5 : 0;
}

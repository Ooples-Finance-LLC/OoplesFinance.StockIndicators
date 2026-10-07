namespace OoplesFinance.StockIndicators.Helpers;

// Distinct component values cast distinct votes, including subnormals.
internal static class TechnicalRatingComparison
{
    internal static int Compare(double left, double right) => left.CompareTo(right);
    internal static int Compare(TechnicalRatingValue left, TechnicalRatingValue right) => left.Units.CompareTo(right.Units);
}

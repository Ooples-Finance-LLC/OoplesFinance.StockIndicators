namespace OoplesFinance.StockIndicators.Helpers;

internal static class BayesianProbability
{
    // Combine two binary evidence probabilities with equal prior odds.
    // Contradictory certain evidence has no normalizable mass; publish zero.
    internal static double Combine(double first, double second)
    {
        var joint = first * second;
        var complement = (1 - first) * (1 - second);
        var total = joint + complement;
        return total == 0 ? 0 : joint / total;
    }
}

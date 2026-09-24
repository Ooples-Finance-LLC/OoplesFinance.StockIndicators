namespace OoplesFinance.StockIndicators.Helpers;

/// <summary>Spearman correlation of prices with chronological positions, using average ranks for ties.</summary>
internal static class ChronologicalSpearman
{
    // Scratch arrays are filled in chronological order by the caller and reused between bars.
    internal static double Compute(double[] prices, double[] positions)
    {
        var count = prices.Length;
        if (count < 2) return 0;
        for (var i = 0; i < count; i++) positions[i] = i;
        Array.Sort(prices, positions);
        var center = (count - 1d) / 2;
        double covariance = 0, rankVariance = 0;
        for (var first = 0; first < count;)
        {
            var end = first + 1;
            while (end < count && prices[end] == prices[first]) end++;
            var centeredRank = (first + end - 1d) / 2 - center;
            rankVariance += (end - first) * centeredRank * centeredRank;
            for (var j = first; j < end; j++) covariance += (positions[j] - center) * centeredRank;
            first = end;
        }
        var timeVariance = count * (count * (double)count - 1) / 12;
        return rankVariance == 0 ? 0 : Math.Max(-1, Math.Min(1, covariance / Math.Sqrt(timeVariance * rankVariance)));
    }
}

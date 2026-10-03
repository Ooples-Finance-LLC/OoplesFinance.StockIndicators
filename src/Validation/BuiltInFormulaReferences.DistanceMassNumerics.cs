using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoundedDistanceMassMean(IReadOnlyList<Bar> bars, int length, bool reciprocal = false)
    {
        var result = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var prices = Enumerable.Range(0, length).Select(lag => ReferenceFraction.FromDouble(i >= lag ? bars[i - lag].Close : 0)).ToArray();
            var numerator = new ReferenceFraction(0);
            var denominator = new ReferenceFraction(0);
            // Pairwise rational distances independently check production's sorted
            // integer prefix sums and compressed startup zeros.
            var distances = prices.Select(price => prices.Aggregate(new ReferenceFraction(0),
                (sum, other) => sum + (price - other).Abs())).ToArray();
            var minimum = distances[0];
            foreach (var distance in distances) if (distance.CompareTo(minimum) < 0) minimum = distance;
            for (var j = 0; j < prices.Length; j++)
            {
                var weight = reciprocal && minimum.Sign != 0
                    ? ReferenceFraction.FromDouble((minimum / distances[j]).ToDouble()) : distances[j];
                numerator += prices[j] * weight;
                denominator += weight;
            }
            result[i] = denominator.Sign == 0 ? bars[i].Close : (numerator / denominator).ToDouble();
        }
        return result;
    }
}

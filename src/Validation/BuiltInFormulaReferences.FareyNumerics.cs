using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoundedFareyMean(IReadOnlyList<Bar> bars, int length)
    {
        int Gcd(int a, int b) { while (b != 0) { var remainder = a % b; a = b; b = remainder; } return a; }
        // Enumerate coprime fractions and order by integer cross-products rather
        // than sharing the production successor recurrence.
        var fractions = new List<(int Numerator, int Denominator)>();
        for (var d = 1; d <= length; d++)
            for (var n = 1; n <= d; n++)
                if (Gcd(n, d) == 1) fractions.Add((n, d));
        fractions.Sort((left, right) => ((long)right.Numerator * left.Denominator)
            .CompareTo((long)left.Numerator * right.Denominator));
        var weights = fractions.Select(f => ReferenceFraction.FromDouble(Math.Round((double)f.Numerator / f.Denominator, 3))).ToArray();
        var denominator = weights.Aggregate(new ReferenceFraction(0), (sum, weight) => sum + weight);
        var result = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var numerator = new ReferenceFraction(0);
            for (var lag = 0; lag < weights.Length && lag <= i; lag++)
                numerator += ReferenceFraction.FromDouble(bars[i - lag].Close) * weights[lag];
            result[i] = (numerator / denominator).ToDouble();
        }
        return result;
    }
}

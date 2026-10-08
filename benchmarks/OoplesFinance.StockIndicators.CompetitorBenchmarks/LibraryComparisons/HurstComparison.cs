using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class HurstComparison
{
    internal static readonly IndicatorErrorBudget Budget = new(1e-10, 1e-10);

    internal static ComparisonSeries Series(IEnumerable<HurstValue> values)
    {
        var rows = values.ToArray();
        return RetrospectivePriceComparison.Series(
            ["HurstExponent", "HurstExponentAL"],
            [
                rows.Select(r => r.HurstExponent).ToArray(),
                rows.Select(r => r.HurstExponentAL).ToArray(),
            ]
        );
    }

    internal static ComparisonPair Pair() =>
        new(
            "Skender.GetHurst",
            nameof(HurstSnapshot),
            (d, p) =>
                Series(
                    d.Quotes.GetHurst(p)
                        .Select(r => new HurstValue(r.HurstExponent, r.HurstExponentAL))
                ),
            (d, p) => Series(HurstSnapshot.Calculate(d.IndicatorBars, p)),
            (d, p) => Series(Reference(d.Closes, p)),
            ["HurstExponent", "HurstExponentAL"],
            CompetitorReference: (d, p) =>
                Series(Reference(d.Quotes.Select(q => (double)q.Close).ToArray(), p)),
            ErrorBudget: Budget
        );

    internal static HurstValue[] Reference(double[] prices, int period)
    {
        var result = Enumerable
            .Range(0, prices.Length)
            .Select(_ => new HurstValue(null, null))
            .ToArray();
        for (var end = period; end < prices.Length; end++)
        {
            var returns = Enumerable
                .Range(end - period + 1, period)
                .Select(i =>
                {
                    if (prices[i] <= 0 || prices[i - 1] <= 0)
                        return double.NaN;
                    var ratio = prices[i] / prices[i - 1];
                    return ratio > 0 && double.IsFinite(ratio)
                        ? Math.Log(ratio)
                        : Math.Log(prices[i]) - Math.Log(prices[i - 1]);
                })
                .ToArray();
            var points = new List<(double X, double Y, double Z)>();
            for (var count = 1; count <= 32 && period / count >= 8; count *= 2)
            {
                var size = period / count;
                double sum = 0;
                foreach (var part in returns.Skip(period - size * count).Chunk(size))
                {
                    var mean = part.Sum() / size;
                    var centered = part.Select(v => v - mean).ToArray();
                    var cumulative = centered
                        .Select((_, i) => centered.Take(i + 1).Sum())
                        .ToArray();
                    var deviation = Math.Sqrt(centered.Select(v => v * v).Sum() / size);
                    sum += deviation == 0 ? 0 : (cumulative.Max() - cumulative.Min()) / deviation;
                }
                var factor =
                    size <= 340
                        ? Math.Exp(
                            LogGamma((size - 1d) / 2) - Math.Log(Math.PI) / 2 - LogGamma(size / 2d)
                        )
                        : Math.Sqrt(2 / (Math.PI * (size - 1d)));
                var expected =
                    factor
                    * Enumerable.Range(1, size - 1).Sum(j => Math.Sqrt((size - j) / (double)j));
                var corrected = sum / count + Math.Sqrt(Math.PI * size / 2) - expected;
                points.Add((Math.Log10(size), Math.Log10(sum / count), Math.Log10(corrected)));
            }
            double? Fit(bool corrected)
            {
                var x = points.Select(p => p.X).ToArray();
                var y = points.Select(p => corrected ? p.Z : p.Y).ToArray();
                double numerator = 0,
                    denominator = 0;
                // Pairwise regression is independent of the production centered sums.
                for (var a = 0; a < x.Length; a++)
                for (var b = a + 1; b < x.Length; b++)
                {
                    numerator += (x[a] - x[b]) * (y[a] - y[b]);
                    denominator += (x[a] - x[b]) * (x[a] - x[b]);
                }
                var value = numerator / denominator;
                return double.IsFinite(value) ? value : null;
            }
            result[end] = new(Fit(false), Fit(true));
        }
        return result;
    }

    private static double LogGamma(double z)
    {
        double[] c =
        [
            0.99999999999980993,
            676.5203681218851,
            -1259.1392167224028,
            771.32342877765313,
            -176.61502916214059,
            12.507343278686905,
            -.13857109526572012,
            9.9843695780195716e-6,
            1.5056327351493116e-7,
        ];
        z -= 1;
        var sum = c[0];
        for (var i = 1; i < c.Length; i++)
            sum += c[i] / (z + i);
        var t = z + 7.5;
        return .5 * Math.Log(2 * Math.PI) + (z + .5) * Math.Log(t) - t + Math.Log(sum);
    }
}

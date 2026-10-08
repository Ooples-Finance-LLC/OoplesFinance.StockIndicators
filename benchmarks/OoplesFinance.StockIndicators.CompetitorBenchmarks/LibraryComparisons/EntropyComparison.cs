using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class EntropyComparison
{
    internal static readonly ComparisonPair Pair = new(
        "QuanTAlib.Entropy",
        nameof(NormalizedWindowEntropy),
        Native,
        (d, p) => Owned(d.IndicatorBars, p),
        (d, p) => Reference(d.Closes, p),
        ErrorBudget: new IndicatorErrorBudget(0, 4e-15, true)
    );

    private static ComparisonSeries Native(CompetitorData data, int period)
    {
        var indicator = new QuanTAlib.Entropy(period);
        return VolumePriceComparison.Mask(
            data.Closes.Select(x =>
                    (double?)indicator.Calc(new QuanTAlib.TValue(x, true, false)).Value
                )
                .ToArray()
        );
    }

    internal static ComparisonSeries Owned(IReadOnlyList<Bar> bars, int period)
    {
        var indicator = new NormalizedWindowEntropy(period);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return VolumePriceComparison.Mask(
            run[indicator.Value].ToArray().Select(x => (double?)x).ToArray()
        );
    }

    internal static ComparisonSeries Reference(double[] prices, int period)
    {
        var result = new double?[prices.Length];
        var logarithms = new Dictionary<int, BigInteger>();
        BigInteger Log(int n)
        {
            if (!logarithms.TryGetValue(n, out var value))
                logarithms[n] = value = LogCount(n);
            return value;
        }
        for (var i = 0; i < prices.Length; i++)
        {
            var window = prices[Math.Max(0, i - period + 1)..(i + 1)];
            // Sorting and run counting is independent of production's mutable frequency table.
            Array.Sort(window);
            var counts = new List<int>();
            for (var position = 0; position < window.Length; position++)
            {
                if (position == 0 || !window[position].Equals(window[position - 1])) // NOSONAR: Exact equality defines frequency groups; a tolerance would merge distinct closes and change entropy.
                    counts.Add(1);
                else
                    counts[^1]++;
            }
            if (counts.Count == 1)
                result[i] = 1;
            else
            {
                // Entropy in count space: log(n) - sum(count*log(count))/n.
                var weighted = counts.Aggregate(
                    BigInteger.Zero,
                    (sum, count) => sum + count * Log(count)
                );
                result[i] = MoneyFlowReferenceArithmetic.Round(
                    window.Length * Log(window.Length) - weighted,
                    window.Length * Log(counts.Count)
                );
            }
        }
        return VolumePriceComparison.Mask(result);
    }

    private static BigInteger LogCount(int n)
    {
        var scale = BigInteger.One << 192;
        BigInteger Series(BigInteger z)
        {
            var square = z * z / scale;
            var term = z;
            var sum = z;
            for (var k = 3; k <= 255; k += 2)
            {
                term = term * square / scale;
                sum += term / k;
            }
            return 2 * sum;
        }
        var exponent = BitOperations.Log2((uint)n);
        var power = BigInteger.One << exponent;
        return Series((n - power) * scale / (n + power)) + exponent * Series(scale / 3);
    }

    internal static CompetitorData Fixture() =>
        CompetitorData.FromCloses([1, 1, 1, 2, 1, 3, 3, 3, 3, 3, 2, 1, 2, 2, 2, 2, 2]);
}

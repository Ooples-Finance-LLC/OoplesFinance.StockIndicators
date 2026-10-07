using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class MedianAdaptiveComparison
{
    internal static ComparisonPair Pair(double threshold = .002) =>
        new(
            "QuanTAlib.Maaf",
            nameof(MedianAdaptiveSnapshot),
            (d, p) =>
            {
                var native = new QuanTAlib.Maaf(p, threshold);
                return new(
                    0,
                    d.Closes.Select(v => native.Calc(new QuanTAlib.TValue(v, true, false)).Value)
                        .ToArray()
                );
            },
            (d, p) =>
                new(0, MedianAdaptiveSnapshot.Calculate(d.IndicatorBars, p, threshold).ToArray()),
            (d, p) => new(0, Reference(d.Closes, p, threshold, false)),
            MinimumInputCount: 0,
            CompetitorReference: (d, p) => new(0, Reference(d.Closes, p, threshold, true)),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static double[] Reference(double[] prices, int period, double threshold, bool native)
    {
        var smooth = new double[prices.Length];
        var output = prices.ToArray();
        var filtered = 0d;
        var priorCandidate = 0d;
        double Update(double current, double previous, int length)
        {
            if (native)
                return Add(Multiply(Divide(2, length + 1d), Subtract(current, previous)), previous);
            return Round(2 * Units(current) + (length - 1) * Units(previous), Grid * (length + 1L));
        }
        for (var i = 3; i < prices.Length; i++)
        {
            smooth[i] = native
                ? Divide(
                    Add(
                        Add(Add(prices[i], Multiply(2, prices[i - 1])), Multiply(2, prices[i - 2])),
                        prices[i - 3]
                    ),
                    6
                )
                : Round(
                    Units(prices[i - 3])
                        + 2 * Units(prices[i - 2])
                        + 2 * Units(prices[i - 1])
                        + Units(prices[i]),
                    6 * Grid
                );
            if (i - 2 < period)
            {
                output[i] = smooth[i];
                continue;
            }
            var candidate = priorCandidate;
            var chosen = period;
            var numerator = Units(.2);
            var denominator = Grid;
            var nativeError = .2;
            while (
                chosen > 0
                && (
                    native
                        ? nativeError > threshold
                        : numerator * Grid > Units(threshold) * denominator
                )
            )
            {
                // A count-based upper order statistic avoids production's sorted selection.
                var window = smooth.Skip(i + 1 - chosen).Take(chosen).ToArray();
                var median = window.First(v =>
                    window.Count(x => x < v) <= chosen / 2 && window.Count(x => x <= v) > chosen / 2
                );
                candidate = Update(smooth[i], priorCandidate, chosen);
                if (!median.Equals(0d))
                {
                    if (native)
                        nativeError = Divide(Math.Abs(Subtract(median, candidate)), median);
                    else
                    {
                        numerator =
                            BigInteger.Abs(Units(median) - Units(candidate)) * Math.Sign(median);
                        denominator = BigInteger.Abs(Units(median));
                    }
                }
                chosen -= 2;
            }
            filtered = Update(smooth[i], filtered, Math.Max(3, chosen));
            priorCandidate = candidate;
            output[i] = filtered;
        }
        return output;
    }
}

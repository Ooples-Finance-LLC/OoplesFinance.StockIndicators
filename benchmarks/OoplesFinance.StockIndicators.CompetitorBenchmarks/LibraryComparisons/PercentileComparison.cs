using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using T = Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class PercentileComparison
{
    internal static readonly ComparisonPair[] Pairs =
    [
        Create(true, true),
        Create(true, false),
        Create(false, true),
        Create(false, false),
    ];

    internal static ComparisonPair Create(bool quan, bool median, double fraction = 0.25) =>
        new(
            (quan ? "QuanTAlib." : "Trady.Indicator.") + (median ? "Median" : "Percentile"),
            "RollingPercentile",
            (data, period) => Competitor(data, period, quan, median, median ? 0.5 : fraction),
            (data, period) => Ooples(data, period, quan, median ? 0.5 : fraction),
            (data, period) => Reference(data, period, quan, median ? 0.5 : fraction, false),
            CompetitorReference: (data, period) =>
                Reference(data, period, quan, median ? 0.5 : fraction, true, median)
        );

    private static ComparisonSeries Ooples(
        CompetitorData data,
        int period,
        bool quan,
        double fraction
    )
    {
        var indicator = new RollingPercentile(period, fraction, quan);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return new(
            quan ? 0 : Math.Min(period - 1, data.Count),
            run[indicator.Outputs[0]].ToArray()
        );
    }

    private static ComparisonSeries Competitor(
        CompetitorData data,
        int period,
        bool quan,
        bool median,
        double fraction
    )
    {
        if (quan)
        {
            QuanTAlib.AbstractBase indicator = median
                ? new QuanTAlib.Median(period)
                : new QuanTAlib.Percentile(period, fraction * 100);
            return new(
                0,
                data.Closes.Select(v => indicator.Calc(new QuanTAlib.TValue(v, true, false)).Value)
                    .ToArray()
            );
        }
        var rows = median
            ? new T.Median(data.Candles, period).Compute()
            : new T.Percentile(data.Candles, period, (decimal)fraction).Compute();
        return new(
            Math.Min(period - 1, data.Count),
            rows.Select(v => (double?)v.Tick ?? double.NaN).ToArray()
        );
    }

    private static ComparisonSeries Reference(
        CompetitorData data,
        int period,
        bool quan,
        double fraction,
        bool native,
        bool median = false
    )
    {
        var first = quan ? 0 : Math.Min(period - 1, data.Count);
        var result = Enumerable.Repeat(double.NaN, data.Count).ToArray();
        var grid = MoneyFlowReferenceArithmetic.Grid;
        var rankUnits = MoneyFlowReferenceArithmetic.Units(fraction);
        for (var i = first; i < data.Count; i++)
        {
            var count = Math.Min(i + 1, period);
            var sorted = data.Closes.Skip(i + 1 - count).Take(count).Order().ToArray();
            if (quan && count < period)
            {
                var sum = sorted.Aggregate(
                    BigInteger.Zero,
                    (s, v) => s + MoneyFlowReferenceArithmetic.Units(v)
                );
                result[i] = MoneyFlowReferenceArithmetic.Round(sum, grid * count);
            }
            else if (native && !quan)
            {
                var prices = sorted.Select(v => (decimal)v).ToArray();
                var rank = (decimal)fraction * (count - 1);
                var lower = (int)rank;
                result[i] = (double)(
                    prices[lower]
                    + (prices[Math.Min(lower + 1, count - 1)] - prices[lower]) * (rank - lower)
                );
            }
            else if (native && median)
            {
                result[i] =
                    count % 2 == 1
                        ? sorted[count / 2]
                        : MoneyFlowReferenceArithmetic.Divide(
                            MoneyFlowReferenceArithmetic.Add(
                                sorted[count / 2 - 1],
                                sorted[count / 2]
                            ),
                            2
                        );
            }
            else if (native)
            {
                var percent = MoneyFlowReferenceArithmetic.Multiply(fraction, 100);
                var rank = MoneyFlowReferenceArithmetic.Multiply(
                    MoneyFlowReferenceArithmetic.Divide(percent, 100),
                    count - 1
                );
                var lower = (int)rank;
                var upper = Math.Min(lower + 1, count - 1);
                var weight = MoneyFlowReferenceArithmetic.Subtract(rank, lower);
                result[i] = MoneyFlowReferenceArithmetic.Add(
                    sorted[lower],
                    MoneyFlowReferenceArithmetic.Multiply(
                        MoneyFlowReferenceArithmetic.Subtract(sorted[upper], sorted[lower]),
                        weight
                    )
                );
            }
            else
            {
                var lower = (int)
                    BigInteger.DivRem(rankUnits * (count - 1), grid, out var remainder);
                var low = MoneyFlowReferenceArithmetic.Units(sorted[lower]);
                var high = MoneyFlowReferenceArithmetic.Units(
                    sorted[Math.Min(lower + 1, count - 1)]
                );
                result[i] = MoneyFlowReferenceArithmetic.Round(
                    low * (grid - remainder) + high * remainder,
                    grid * grid
                );
            }
        }
        return new(first, result);
    }

    internal static CompetitorData Fixture() =>
        CompetitorData.FromCloses([9, -3, 4, 4, 18, 0, -8, 9, 9, 1, 20, -30, 0]);
}

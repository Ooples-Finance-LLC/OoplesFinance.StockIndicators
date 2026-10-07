using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class ReverseWilderComparison
{
    internal static readonly ComparisonPair Pair = new(
        "QuanTAlib.Rma",
        "ReverseWilderAverage",
        Native,
        Owned,
        (d, p) => Reference(d, p, false),
        CompetitorReference: (d, p) => Reference(d, p, true)
    );

    private static ComparisonSeries Native(CompetitorData data, int period)
    {
        var indicator = new QuanTAlib.Rma(period);
        return new(
            0,
            data.Closes.Select(v => indicator.Calc(new QuanTAlib.TValue(v, true, false)).Value)
                .ToArray()
        );
    }

    private static ComparisonSeries Owned(CompetitorData data, int period)
    {
        var indicator = new ReverseWilderAverage(period);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return new(0, run[indicator.Outputs[0]].ToArray());
    }

    private static ComparisonSeries Reference(CompetitorData data, int period, bool native)
    {
        var result = new double[data.Count];
        var alpha = Divide(1, period);
        for (var i = 0; i < data.Count; i++)
        {
            if (i == 0)
            {
                result[i] = data.Closes[i];
                continue;
            }
            var previous = result[i - 1];
            var current = data.Closes[i];
            if (native)
                result[i] =
                    i < period
                        ? Divide(Add(Multiply(previous, i), current), i + 1)
                        : Add(Multiply(alpha, Subtract(previous, current)), previous);
            else
            {
                // Independent fixed-grid recurrence; retain only the published previous value.
                var numerator =
                    i < period
                        ? Units(previous) * i + Units(current)
                        : Units(previous) * (new BigInteger(period) + 1) - Units(current);
                result[i] = Round(numerator, Grid * (i < period ? i + 1 : period));
            }
        }
        return new(0, result);
    }

    internal static CompetitorData Fixture() =>
        CompetitorData.FromCloses([3, 12, -6, 9, 18, -3, 4, 4, -18, 20]);

    internal static CompetitorData RoundedSeedFixture() =>
        CompetitorData.FromCloses([1e16, 1, -1e16, 3]);
}

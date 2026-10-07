using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class FixedWeightedComparison
{
    internal static readonly ComparisonPair[] Pairs = [Create(false), Create(true)];

    private static ComparisonPair Create(bool twice) =>
        new(
            twice ? "QuanTAlib.Dwma" : "QuanTAlib.Wma",
            "FixedPeriodWma",
            (d, p) => Native(d, p, twice),
            (d, p) => Owned(d, p, twice),
            (d, p) => Reference(d, p, twice, false),
            CompetitorReference: (d, p) => Reference(d, p, twice, true)
        );

    private static ComparisonSeries Native(CompetitorData data, int period, bool twice)
    {
        QuanTAlib.AbstractBase indicator = twice
            ? new QuanTAlib.Dwma(period)
            : new QuanTAlib.Wma(period);
        return new(
            0,
            data.Closes.Select(v => indicator.Calc(new QuanTAlib.TValue(v, true, false)).Value)
                .ToArray()
        );
    }

    private static ComparisonSeries Owned(CompetitorData data, int period, bool twice)
    {
        var indicator = new FixedPeriodWma(period);
        if (twice)
            indicator.Of(new FixedPeriodWma(period));
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return new(0, run[indicator.Outputs[0]].ToArray());
    }

    private static ComparisonSeries Reference(
        CompetitorData data,
        int period,
        bool twice,
        bool native
    )
    {
        var first = Stage(data.Closes, period, native);
        return new(0, twice ? Stage(first, period, native) : first);
    }

    internal static double[] Stage(double[] inputs, int period, bool native)
    {
        var result = new double[inputs.Length];
        // Quan's kernel first rounds this unchecked integer product, then normalizes
        // each available kernel prefix again before multiplying the prices.
        var nativeMass = Divide(unchecked(period * (period + 1)), 2);
        for (var i = 0; i < inputs.Length; i++)
        {
            var count = Math.Min(period, i + 1);
            if (native)
            {
                var weights = Enumerable
                    .Range(0, count)
                    .Select(lag => Divide(period - lag, nativeMass))
                    .ToArray();
                var mass = weights.Aggregate(0d, Add);
                var total = 0d;
                for (var lag = 0; lag < count; lag++)
                    total = Add(total, Multiply(inputs[i - lag], Divide(weights[lag], mass)));
                result[i] = total;
            }
            else
            {
                var total = BigInteger.Zero;
                long mass = 0;
                for (var lag = 0; lag < count; lag++)
                {
                    total += Units(inputs[i - lag]) * (period - lag);
                    mass += period - lag;
                }
                result[i] = Round(total, Grid * mass);
            }
        }
        return result;
    }

    internal static CompetitorData Fixture() =>
        CompetitorData.FromCloses([3, 12, -6, 9, 18, -3, 4, 4, -18, 20]);
}

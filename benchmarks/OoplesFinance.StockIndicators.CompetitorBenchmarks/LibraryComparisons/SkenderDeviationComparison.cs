using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class SkenderDeviationComparison
{
    internal static readonly string[] Names = ["StdDev", "Mean", "ZScore", "StdDevSma"];
    internal static readonly ComparisonPair Pair = Create(20);

    internal static ComparisonPair Create(int? smooth) =>
        new(
            "Skender.GetStdDev",
            "StandardDeviationWithDetails",
            (d, p) => Native(d, p, smooth),
            (d, p) => Ooples(d, p, smooth),
            (d, p) => Reference(d, p, smooth, false),
            Names,
            CompetitorReference: (d, p) => Reference(d, p, smooth, true)
        );

    private static ComparisonSeries Series(double?[][] values) =>
        new(
            Names
                .Select(
                    (name, slot) =>
                        new KeyValuePair<string, ComparisonOutput>(
                            name,
                            new(
                                0,
                                values[slot].Select(v => v ?? double.NaN).ToArray(),
                                values[slot].Select(v => v.HasValue).ToArray()
                            )
                        )
                )
                .ToDictionary(kv => kv.Key, kv => kv.Value)
        );

    private static ComparisonSeries Native(CompetitorData data, int period, int? smooth)
    {
        var rows = data.Quotes.GetStdDev(period, smooth).ToArray();
        return Series([
            rows.Select(r => r.StdDev).ToArray(),
            rows.Select(r => r.Mean).ToArray(),
            rows.Select(r => r.ZScore).ToArray(),
            rows.Select(r => r.StdDevSma).ToArray(),
        ]);
    }

    private static ComparisonSeries Ooples(CompetitorData data, int period, int? smooth)
    {
        var indicator = new StandardDeviationWithDetails(period, smooth);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return Series(
            Enumerable
                .Range(0, 4)
                .Select(slot =>
                {
                    var values = run[indicator.Outputs[slot]].ToArray();
                    var present = run[indicator.Outputs[slot + 4]].ToArray();
                    return values.Select((v, i) => present[i] > 0 ? (double?)v : null).ToArray();
                })
                .ToArray()
        );
    }

    private static ComparisonSeries Reference(
        CompetitorData data,
        int period,
        int? smooth,
        bool native
    )
    {
        var values = Enumerable.Range(0, 4).Select(_ => new double?[data.Count]).ToArray();
        for (var i = period - 1; i < data.Count; i++)
        {
            if (native)
                NativeWindow(data, period, i, values);
            else
                ExactWindow(data, period, i, values);
            if (smooth is not int length || (long)i < (long)period + length - 2)
                continue;
            if (native)
            {
                double sum = 0;
                for (var j = i - length + 1; j <= i; j++)
                    sum = Add(sum, values[0][j]!.Value);
                values[3][i] = Divide(sum, length);
            }
            else
            {
                var sum = BigInteger.Zero;
                for (var j = i - length + 1; j <= i; j++)
                    sum += Units(values[0][j]!.Value);
                values[3][i] = Round(sum, length * Grid);
            }
        }
        return Series(values);
    }

    private static void ExactWindow(CompetitorData data, int period, int index, double?[][] values)
    {
        var window = data.Closes.Skip(index - period + 1).Take(period).Select(Units).ToArray();
        var n = new BigInteger(period);
        var sum = window.Aggregate(BigInteger.Zero, (a, b) => a + b);
        var deviations = window.Select(v => n * v - sum).ToArray();
        var squared = deviations.Aggregate(BigInteger.Zero, (a, b) => a + b * b);
        values[0][index] = DispersionReferenceArithmetic.Sqrt(squared, n * n * n * Grid * Grid);
        values[1][index] = Round(sum, n * Grid);
        if (!squared.IsZero)
            values[2][index] =
                deviations[^1].Sign
                * DispersionReferenceArithmetic.Sqrt(deviations[^1] * deviations[^1] * n, squared);
    }

    // Package quote conversion, binary64 centered deviations, and staged normalization.
    private static void NativeWindow(CompetitorData data, int period, int index, double?[][] values)
    {
        var window = data
            .Closes.Skip(index - period + 1)
            .Take(period)
            .Select(v => (double)(decimal)v)
            .ToArray();
        var mean = Divide(window.Aggregate(0d, Add), period);
        var squared = window.Aggregate(
            0d,
            (a, b) => Add(a, Multiply(Subtract(b, mean), Subtract(b, mean)))
        );
        var deviation = Math.Sqrt(Divide(squared, period));
        values[0][index] = deviation;
        values[1][index] = mean;
        if (deviation > 0)
            values[2][index] = Divide(Subtract(window[^1], mean), deviation);
    }

    internal static CompetitorData FlatRoundingFixture() =>
        CompetitorData.FromCloses([.1, .1, .1, .1, .1, 2, .1, .1, .1]);

    internal static CompetitorData CollapsedQuoteFixture() =>
        CompetitorData.FromCloses([.1, Math.BitIncrement(.1), .1, Math.BitIncrement(.1)]);
}

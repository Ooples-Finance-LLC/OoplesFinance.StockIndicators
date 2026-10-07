using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class HeikinAshiComparison
{
    internal static readonly string[] Names = ["Open", "High", "Low", "Close", "Volume"];
    internal static readonly ComparisonPair Pair = new(
        "Skender.GetHeikinAshi",
        nameof(HeikinAshiCandles),
        Native,
        Owned,
        (d, _) => Reference(d, false),
        Names,
        CompetitorReference: (d, _) => Reference(d, true),
        ErrorBudget: IndicatorErrorBudget.Exact
    );

    private static ComparisonSeries Series(double[][] values) =>
        new(
            Names
                .Select(
                    (name, slot) =>
                        new KeyValuePair<string, ComparisonOutput>(
                            name,
                            new(
                                0,
                                values[slot],
                                Enumerable.Repeat(true, values[slot].Length).ToArray()
                            )
                        )
                )
                .ToDictionary(x => x.Key, x => x.Value)
        );

    private static ComparisonSeries Native(CompetitorData d, int _)
    {
        var rows = d.Quotes.GetHeikinAshi().ToArray();
        return Series([
            rows.Select(r => (double)r.Open).ToArray(),
            rows.Select(r => (double)r.High).ToArray(),
            rows.Select(r => (double)r.Low).ToArray(),
            rows.Select(r => (double)r.Close).ToArray(),
            rows.Select(r => (double)r.Volume).ToArray(),
        ]);
    }

    private static ComparisonSeries Owned(CompetitorData d, int _)
    {
        var indicator = new HeikinAshiCandles();
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(d.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return Series(indicator.Outputs.Select(o => run[o].ToArray()).ToArray());
    }

    private static ComparisonSeries Reference(CompetitorData data, bool native)
    {
        var result = Enumerable.Range(0, 5).Select(_ => new double[data.Count]).ToArray();
        if (native)
        {
            // Decimal quote conversion and decimal operator rounding are part of
            // the native contract. Keep those stages separate from binary64 means.
            var closes = data
                .Quotes.Select(q =>
                    new[] { q.Open, q.High, q.Low, q.Close }.Aggregate(0m, (a, b) => a + b) / 4m
                )
                .ToArray();
            var opens = new decimal[data.Count];
            for (var i = 0; i < data.Count; i++)
            {
                var q = data.Quotes[i];
                opens[i] = (i == 0 ? q.Open + q.Close : opens[i - 1] + closes[i - 1]) / 2m;
                result[0][i] = (double)opens[i];
                result[1][i] = (double)new[] { q.High, opens[i], closes[i] }.Order().Last();
                result[2][i] = (double)new[] { q.Low, opens[i], closes[i] }.Order().First();
                result[3][i] = (double)closes[i];
                result[4][i] = (double)q.Volume;
            }
        }
        else
        {
            var closes = Enumerable
                .Range(0, data.Count)
                .Select(i =>
                    Round(
                        new[]
                        {
                            data.Opens[i],
                            data.Highs[i],
                            data.Lows[i],
                            data.Closes[i],
                        }.Aggregate(BigInteger.Zero, (sum, v) => sum + Units(v)),
                        4 * Grid
                    )
                )
                .ToArray();
            for (var i = 0; i < data.Count; i++)
            {
                var open = Round(
                    i == 0
                        ? Units(data.Opens[i]) + Units(data.Closes[i])
                        : Units(result[0][i - 1]) + Units(closes[i - 1]),
                    2 * Grid
                );
                result[0][i] = open;
                result[1][i] = new[] { data.Highs[i], open, closes[i] }.Max();
                result[2][i] = new[] { data.Lows[i], open, closes[i] }.Min();
                result[3][i] = closes[i];
                result[4][i] = data.Volumes[i];
            }
        }
        return Series(result);
    }

    internal static CompetitorData Fixture() =>
        CompetitorData.FromOhlcv(
            [2, 8, -4, 0, 3],
            [4, 10, -1, 0, 7],
            [1, 6, -8, 0, -2],
            [3, 7, -6, 0, 4],
            [11, 0, 20, 30, 40]
        );
}

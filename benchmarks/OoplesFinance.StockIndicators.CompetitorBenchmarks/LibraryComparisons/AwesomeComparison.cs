using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class AwesomeComparison
{
    internal static readonly string[] Names = ["Oscillator", "Normalized"];
    internal static readonly ComparisonPair Pair = Create(5, 34);

    internal static ComparisonPair Create(int fast, int slow) =>
        new(
            "Skender.GetAwesome",
            nameof(AwesomeWithDetails),
            (d, _) => Native(d, fast, slow),
            (d, _) => Owned(d, fast, slow),
            (d, _) => Reference(d, fast, slow, false),
            Names,
            CompetitorReference: (d, _) => Reference(d, fast, slow, true),
            ErrorBudget: IndicatorErrorBudget.Exact
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
                .ToDictionary(x => x.Key, x => x.Value)
        );

    private static ComparisonSeries Native(CompetitorData data, int fast, int slow)
    {
        var rows = data.Quotes.GetAwesome(fast, slow).ToArray();
        return Series([
            rows.Select(r => r.Oscillator).ToArray(),
            rows.Select(r => r.Normalized).ToArray(),
        ]);
    }

    private static ComparisonSeries Owned(CompetitorData data, int fast, int slow)
    {
        var indicator = new AwesomeWithDetails(fast, slow);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return Series(
            Enumerable
                .Range(0, 2)
                .Select(slot =>
                {
                    var flags = run[indicator.Outputs[slot + 2]].ToArray();
                    return run[indicator.Outputs[slot]]
                        .ToArray()
                        .Select((v, i) => flags[i] > 0 ? (double?)v : null)
                        .ToArray();
                })
                .ToArray()
        );
    }

    private static ComparisonSeries Reference(CompetitorData data, int fast, int slow, bool native)
    {
        var values = new[] { new double?[data.Count], new double?[data.Count] };
        var prices = Enumerable
            .Range(0, data.Count)
            .Select(i =>
                native
                    ? (double)(data.Quotes[i].High + data.Quotes[i].Low) / 2
                    : Round(Units(data.Highs[i]) + Units(data.Lows[i]), 2 * Grid)
            )
            .ToArray();
        for (var i = slow - 1; i < data.Count; i++)
        {
            double Mean(int period) =>
                native
                    ? Divide(prices.Skip(i - period + 1).Take(period).Aggregate(0d, Add), period)
                    : Round(
                        prices
                            .Skip(i - period + 1)
                            .Take(period)
                            .Aggregate(BigInteger.Zero, (sum, p) => sum + Units(p)),
                        period * Grid
                    );
            var oscillator = Subtract(Mean(fast), Mean(slow));
            values[0][i] = oscillator;
            if (prices[i] != 0)
                values[1][i] = native
                    ? Divide(Multiply(100, oscillator), prices[i])
                    : Round(100 * Units(oscillator), Units(prices[i])); // NOSONAR: Exact domain boundary.
        }
        return Series(values);
    }

    internal static CompetitorData Fixture() =>
        CompetitorData.FromOhlcv(
            [2, 3, -1, 0, 5, 1, 4, 0],
            [4, 6, 2, 1, 8, 3, 5, 1],
            [0, 0, -4, -1, 2, -1, 1, -1],
            [1, 1, -2, 0, 4, 2, 4, 0],
            [1, 1, 1, 1, 1, 1, 1, 1]
        );
}

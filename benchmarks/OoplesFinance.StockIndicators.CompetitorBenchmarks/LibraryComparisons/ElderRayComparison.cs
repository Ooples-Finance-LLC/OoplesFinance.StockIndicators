using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class ElderRayComparison
{
    internal static readonly string[] Names = ["Ema", "BullPower", "BearPower"];
    internal static readonly ComparisonPair Pair = new(
        "Skender.GetElderRay",
        nameof(ElderRayWithDetails),
        Native,
        Owned,
        (d, p) => Reference(d, p, false),
        Names,
        CompetitorReference: (d, p) => Reference(d, p, true),
        ErrorBudget: OoplesFinance.StockIndicators.Validation.IndicatorErrorBudget.Exact
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

    private static ComparisonSeries Native(CompetitorData data, int period)
    {
        var rows = data.Quotes.GetElderRay(period).ToArray();
        return Series([
            rows.Select(r => r.Ema).ToArray(),
            rows.Select(r => r.BullPower).ToArray(),
            rows.Select(r => r.BearPower).ToArray(),
        ]);
    }

    private static ComparisonSeries Owned(CompetitorData data, int period)
    {
        var indicator = new ElderRayWithDetails(period);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var flags = run[indicator.IsDefined].ToArray();
        return Series(
            Enumerable
                .Range(0, 3)
                .Select(slot =>
                    run[indicator.Outputs[slot]]
                        .ToArray()
                        .Select((v, i) => flags[i] > 0 ? (double?)v : null)
                        .ToArray()
                )
                .ToArray()
        );
    }

    private static ComparisonSeries Reference(CompetitorData data, int period, bool native)
    {
        var values = Enumerable.Range(0, 3).Select(_ => new double?[data.Count]).ToArray();
        if (period > data.Count)
            return Series(values);
        var closes = native ? data.Quotes.Select(q => (double)q.Close).ToArray() : data.Closes;
        var highs = native ? data.Quotes.Select(q => (double)q.High).ToArray() : data.Highs;
        var lows = native ? data.Quotes.Select(q => (double)q.Low).ToArray() : data.Lows;
        var ema = native
            ? Divide(closes.Take(period).Aggregate(0d, Add), period)
            : Round(
                closes.Take(period).Aggregate(BigInteger.Zero, (sum, v) => sum + Units(v)),
                Grid * period
            );
        var alpha = Divide(2, (long)period + 1);
        for (var i = period - 1; i < data.Count; i++)
        {
            if (i >= period)
                ema = native
                    ? Add(ema, Multiply(alpha, Subtract(closes[i], ema)))
                    : Round(
                        2 * Units(closes[i]) + (period - 1) * Units(ema),
                        Grid * ((long)period + 1)
                    );
            values[0][i] = ema;
            values[1][i] = Subtract(highs[i], ema);
            values[2][i] = Subtract(lows[i], ema);
        }
        return Series(values);
    }

    internal static CompetitorData Fixture() =>
        CompetitorData.FromOhlcv(
            [1, 4, -3, 10, 2, 8],
            [3, 8, 0, 12, 5, 12],
            [-2, 1, -6, 7, -1, 5],
            [2, 5, -4, 9, 3, 7],
            [1, 1, 1, 1, 1, 1]
        );
}

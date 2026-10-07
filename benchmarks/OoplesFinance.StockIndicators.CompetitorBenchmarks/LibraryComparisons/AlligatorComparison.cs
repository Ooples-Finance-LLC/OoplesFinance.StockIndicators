using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class AlligatorComparison
{
    internal static readonly int[] Default = [13, 8, 8, 5, 5, 3];
    internal static readonly string[] AlligatorNames = ["Jaw", "Teeth", "Lips"];
    internal static readonly string[] GatorNames =
    [
        "Upper",
        "Lower",
        "UpperIsExpanding",
        "LowerIsExpanding",
    ];
    internal static readonly ComparisonPair[] Pairs =
    [
        Create(false, Default),
        Create(true, Default),
    ];

    internal static ComparisonPair Create(bool gator, int[] settings) =>
        new(
            gator ? "Skender.GetGator" : "Skender.GetAlligator",
            gator ? nameof(GatorWithDetails) : nameof(AlligatorWithDetails),
            (d, _) => Native(d, gator, settings),
            (d, _) => Owned(d, gator, settings),
            (d, _) => Reference(d, gator, settings, false),
            gator ? GatorNames : AlligatorNames,
            CompetitorReference: (d, _) => Reference(d, gator, settings, true),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static MultiOutputIndicatorBase Indicator(bool gator, int[] s) =>
        gator
            ? new GatorWithDetails(s[0], s[1], s[2], s[3], s[4], s[5])
            : new AlligatorWithDetails(s[0], s[1], s[2], s[3], s[4], s[5]);

    private static ComparisonSeries Series(bool gator, double?[][] values) =>
        new(
            (gator ? GatorNames : AlligatorNames)
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

    private static ComparisonSeries Native(CompetitorData d, bool gator, int[] s)
    {
        if (gator)
        {
            var rows = (
                s.SequenceEqual(Default)
                    ? d.Quotes.GetGator()
                    : d.Quotes.GetAlligator(s[0], s[1], s[2], s[3], s[4], s[5]).GetGator()
            ).ToArray();
            return Series(
                true,
                [
                    rows.Select(r => r.Upper).ToArray(),
                    rows.Select(r => r.Lower).ToArray(),
                    rows.Select(r => Number(r.UpperIsExpanding)).ToArray(),
                    rows.Select(r => Number(r.LowerIsExpanding)).ToArray(),
                ]
            );
        }
        var alligator = d.Quotes.GetAlligator(s[0], s[1], s[2], s[3], s[4], s[5]).ToArray();
        return Series(
            false,
            [
                alligator.Select(r => r.Jaw).ToArray(),
                alligator.Select(r => r.Teeth).ToArray(),
                alligator.Select(r => r.Lips).ToArray(),
            ]
        );
    }

    internal static double? Number(bool? value) => value.HasValue ? (value.Value ? 1d : 0) : null;

    private static ComparisonSeries Owned(CompetitorData data, bool gator, int[] settings)
    {
        var indicator = Indicator(gator, settings);
        var count = gator ? 4 : 3;
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return Series(
            gator,
            Enumerable
                .Range(0, count)
                .Select(slot =>
                {
                    var presence = run[indicator.Outputs[slot + count]].ToArray();
                    return run[indicator.Outputs[slot]]
                        .ToArray()
                        .Select((v, i) => presence[i] > 0 ? (double?)v : null)
                        .ToArray();
                })
                .ToArray()
        );
    }

    private static ComparisonSeries Reference(
        CompetitorData data,
        bool gator,
        int[] settings,
        bool native
    )
    {
        var prices = Enumerable
            .Range(0, data.Count)
            .Select(i =>
                native
                    ? (double)(data.Quotes[i].High + data.Quotes[i].Low) / 2
                    : Round(Units(data.Highs[i]) + Units(data.Lows[i]), 2 * Grid)
            )
            .ToArray();
        var lines = Enumerable.Range(0, 3).Select(_ => new double?[data.Count]).ToArray();
        for (var slot = 0; slot < 3; slot++)
        {
            var period = settings[2 * slot];
            var offset = settings[2 * slot + 1];
            if ((long)period + offset > data.Count)
                continue;
            var previous = native
                ? Divide(prices.Take(period).Aggregate(0d, Add), period)
                : Round(
                    prices.Take(period).Aggregate(BigInteger.Zero, (sum, x) => sum + Units(x)),
                    period * Grid
                );
            for (var i = period - 1; i < data.Count - offset; i++)
            {
                if (i >= period)
                    previous = native
                        ? Divide(Add(Multiply(previous, period - 1), prices[i]), period)
                        : Round(Units(previous) * (period - 1) + Units(prices[i]), period * Grid);
                lines[slot][i + offset] = previous;
            }
        }
        if (!gator)
            return Series(false, lines);
        var result = Enumerable.Range(0, 4).Select(_ => new double?[data.Count]).ToArray();
        for (var i = 0; i < data.Count; i++)
        {
            for (var slot = 0; slot < 2; slot++)
            {
                if (lines[slot][i] is { } a && lines[slot + 1][i] is { } b)
                    result[slot][i] = (slot == 0 ? 1 : -1) * Math.Abs(Subtract(a, b));
                if (i > 0 && result[slot][i - 1].HasValue)
                    result[slot + 2][i] = Number(
                        slot == 0
                            ? result[slot][i] > result[slot][i - 1]
                            : result[slot][i] < result[slot][i - 1]
                    );
            }
        }
        return Series(true, result);
    }

    internal static CompetitorData Fixture(int count = 93)
    {
        var highs = Enumerable.Range(0, count).Select(i => (i % 9 - 4) * .5 + 2).ToArray();
        var lows = Enumerable.Range(0, count).Select(i => (i % 7 - 3) * .25 - 2).ToArray();
        return CompetitorData.FromOhlcv(
            new double[count],
            highs,
            lows,
            Enumerable.Repeat(.25, count).ToArray(),
            new double[count]
        );
    }
}

using OoplesFinance.StockIndicators.Indicators;
using TC = Trady.Analysis.Candlestick;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class TrendBabyComparison
{
    internal static readonly ComparisonPair[] Pairs = [Pair(true), Pair(false)];

    internal static IIndicator Create(
        bool bullish,
        int trend = 3,
        int period = 20,
        decimal percentile = .75m,
        decimal doji = .1m
    ) =>
        bullish
            ? new BullishAbandonedBabyPattern(trend, period, percentile, doji)
            : new BearishAbandonedBabyPattern(trend, period, percentile, doji);

    internal static ComparisonPair Pair(
        bool bullish,
        int longPeriod = 20,
        decimal percentile = .75m,
        decimal doji = .1m
    ) =>
        new(
            "Trady.Candlestick." + (bullish ? "Bullish" : "Bearish") + "AbandonedBaby",
            bullish ? "BullishAbandonedBabyPattern" : "BearishAbandonedBabyPattern",
            (data, trend) => Competitor(data, bullish, trend, longPeriod, percentile, doji),
            (data, trend) =>
                CandleComparisonExecution.Run(
                    Create(bullish, trend, longPeriod, percentile, doji),
                    data,
                    Math.Min(2, data.Count)
                ),
            (data, trend) => Reference(data, bullish, trend, longPeriod, percentile, doji)
        );

    private static ComparisonSeries Competitor(
        CompetitorData data,
        bool bullish,
        int trend,
        int period,
        decimal percentile,
        decimal doji
    )
    {
        var ticks = bullish
            ? new TC.BullishAbandonedBaby(data.Candles, trend, period, percentile, doji).Compute()
            : new TC.BearishAbandonedBaby(data.Candles, trend, period, percentile, doji).Compute();
        return new(
            Math.Min(2, data.Count),
            ticks
                .Select(t =>
                    t.Tick.HasValue
                        ? t.Tick.Value
                            ? 1d
                            : 0
                        : double.NaN
                )
                .ToArray()
        );
    }

    private static ComparisonSeries Reference(
        CompetitorData data,
        bool bullish,
        int trend,
        int period,
        decimal percentile,
        decimal doji
    )
    {
        var result = new double[data.Count];
        var lengths = data
            .Opens.Select((o, i) => Math.Abs((decimal)o - (decimal)data.Closes[i]))
            .ToArray();
        bool Long(int index, bool white)
        {
            var length = white ? period : 20;
            var q = white ? percentile : .75m;
            if (
                index < length - 1
                || (
                    white
                        ? data.Closes[index] <= data.Opens[index]
                        : data.Closes[index] >= data.Opens[index]
                )
            )
                return false;
            var window = lengths.Skip(index - length + 1).Take(length).Order().ToArray();
            var rank = q * (length - 1);
            var lo = (int)rank;
            var value = window[lo];
            if (lo + 1 < length)
                value += (window[lo + 1] - value) * (rank - lo);
            return lengths[index] >= value;
        }
        for (var i = 2; i < data.Count; i++)
        {
            if (i - 1 < trend)
                continue;
            var trending = Enumerable
                .Range(i - trend, trend)
                .All(j =>
                    bullish
                        ? data.Highs[j] < data.Highs[j - 1] && data.Lows[j] < data.Lows[j - 1]
                        : data.Highs[j] > data.Highs[j - 1] && data.Lows[j] > data.Lows[j - 1]
                );
            var small =
                lengths[i - 1] < ((decimal)data.Highs[i - 1] - (decimal)data.Lows[i - 1]) * doji;
            var gap = bullish
                ? data.Highs[i - 1] < data.Lows[i - 2] && data.Highs[i - 1] < data.Lows[i]
                : data.Lows[i - 1] > data.Highs[i - 2] && data.Lows[i - 1] > data.Highs[i];
            result[i] =
                trending && small && gap && Long(i - 2, !bullish) && Long(i, bullish) ? 1 : 0;
        }
        return new(Math.Min(2, data.Count), result);
    }

    internal sealed record Case(
        string Name,
        double Expected,
        (double O, double H, double L, double C) First,
        (double O, double H, double L, double C) Middle,
        (double O, double H, double L, double C) Last
    );

    internal static readonly Case[] Cases =
    [
        new("signal", 1, (60, 61, 49, 50), (45, 46, 44, 45), (50, 62, 49, 60)),
        new("first gap equality", 0, (60, 61, 46, 50), (45, 46, 44, 45), (50, 62, 49, 60)),
        new("last gap equality", 0, (60, 61, 49, 50), (45, 46, 44, 45), (50, 62, 46, 60)),
        new("doji threshold equality", 0, (60, 61, 49, 50), (40, 46, 36, 41), (50, 62, 49, 60)),
        new(
            "doji just below threshold",
            1,
            (60, 61, 49, 50),
            (40, 46, 36, 40.875),
            (50, 62, 49, 60)
        ),
        new("zero range doji", 0, (60, 61, 49, 50), (45, 45, 45, 45), (50, 62, 49, 60)),
        new("wrong first color", 0, (50, 61, 49, 60), (45, 46, 44, 45), (50, 62, 49, 60)),
        new("wrong last color", 0, (60, 61, 49, 50), (45, 46, 44, 45), (60, 62, 49, 50)),
        new("first short body", 0, (51, 61, 49, 50.5), (45, 46, 44, 45), (50, 62, 49, 60)),
        new("last short body", 0, (60, 61, 49, 50), (45, 46, 44, 45), (50, 62, 49, 50.5)),
        new(
            "no midpoint confirmation required",
            1,
            (60, 61, 49, 50),
            (45, 46, 44, 45),
            (50, 82, 49, 80)
        ),
    ];

    internal static CompetitorData Golden(
        Case item,
        bool bullish,
        int trend = 3,
        int longPeriod = 20
    )
    {
        var bars = new List<(double O, double H, double L, double C)>();
        var count = Math.Max(Math.Max(longPeriod, 20), trend);
        for (var i = count - 1; i >= 0; i--)
            bars.Add((62 + 2d * i, 63 + 2d * i, 60 + 2d * i, 61 + 2d * i));
        bars.Add(item.First);
        bars.Add(item.Middle);
        bars.Add(item.Last);
        if (!bullish)
            bars = bars.Select(b => (200 - b.O, 200 - b.L, 200 - b.H, 200 - b.C)).ToList();
        return CompetitorData.FromOhlc(
            bars.Select(b => b.O).ToArray(),
            bars.Select(b => b.H).ToArray(),
            bars.Select(b => b.L).ToArray(),
            bars.Select(b => b.C).ToArray()
        );
    }

    internal static CompetitorData Fixture(int trend)
    {
        var bars = Cases
            .SelectMany(c =>
                new[] { true, false }.SelectMany(b => Golden(c, b, trend).IndicatorBars)
            )
            .ToArray();
        return CompetitorData.FromOhlc(
            bars.Select(b => b.Open).ToArray(),
            bars.Select(b => b.High).ToArray(),
            bars.Select(b => b.Low).ToArray(),
            bars.Select(b => b.Close).ToArray()
        );
    }
}

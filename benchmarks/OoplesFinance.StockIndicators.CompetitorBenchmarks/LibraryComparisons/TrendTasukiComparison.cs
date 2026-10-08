using OoplesFinance.StockIndicators.Indicators;
using TC = Trady.Analysis.Candlestick;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class TrendTasukiComparison
{
    internal static readonly ComparisonPair[] Pairs = [CreatePair(true), CreatePair(false)];

    internal static IIndicator Create(bool upside, int period) =>
        upside ? new UpsideTasukiGapPattern(period) : new DownsideTasukiGapPattern(period);

    internal static ComparisonPair CreatePair(bool upside, decimal threshold = .1m) =>
        new(
            "Trady.Candlestick." + (upside ? "Upside" : "Downside") + "TasukiGap",
            upside ? "UpsideTasukiGapPattern" : "DownsideTasukiGapPattern",
            (data, period) => Competitor(data, period, upside, threshold),
            (data, period) =>
                CandleComparisonExecution.Run(
                    Create(upside, period),
                    data,
                    Math.Min(2, data.Count)
                ),
            (data, period) => Reference(data, period, upside)
        );

    private static ComparisonSeries Competitor(
        CompetitorData data,
        int period,
        bool upside,
        decimal threshold
    )
    {
        var ticks = upside
            ? new TC.UpsideTasukiGap(data.Candles, period, threshold).Compute()
            : new TC.DownsideTasukiGap(data.Candles, period, threshold).Compute();
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

    private static ComparisonSeries Reference(CompetitorData data, int period, bool upside)
    {
        var result = new double[data.Count];
        var o = data.Opens.Select(v => (decimal)v).ToArray();
        var c = data.Closes.Select(v => (decimal)v).ToArray();
        var h = data.Highs.Select(v => (decimal)v).ToArray();
        var l = data.Lows.Select(v => (decimal)v).ToArray();
        for (var i = 2; i < result.Length; i++)
        {
            if (i - 1 < period)
                continue;
            var direction = upside ? 1 : -1;
            var trend = Enumerable
                .Range(i - period, period)
                .All(j =>
                    Math.Sign(h[j] - h[j - 1]) == direction
                    && Math.Sign(l[j] - l[j - 1]) == direction
                );
            if (
                !trend
                || Math.Sign(c[i - 2] - o[i - 2]) != direction
                || Math.Sign(c[i - 1] - o[i - 1]) != direction
                || Math.Sign(c[i] - o[i]) != -direction
            )
                continue;
            result[i] =
                (
                    upside
                        ? h[i - 2] < l[i - 1]
                            && o[i] > o[i - 1]
                            && o[i] < c[i - 1]
                            && c[i] < o[i - 1]
                        : l[i - 2] > h[i - 1] && c[i] > h[i - 1] && c[i] < l[i - 2]
                )
                    ? 1
                    : 0;
        }
        return new(Math.Min(2, data.Count), result);
    }

    internal sealed record Case(
        bool Upside,
        string Name,
        double Expected,
        (double O, double H, double L, double C) Final
    );

    internal static readonly Case[] Cases =
    [
        new(true, "ordinary gap", 1, (19, 20, 14, 15)),
        new(true, "crosses whole gap", 1, (19, 20, 0, 1)),
        new(true, "open equals second open", 0, (17, 20, 14, 15)),
        new(true, "open equals second close", 0, (21, 22, 14, 15)),
        new(true, "close equals second open", 0, (19, 20, 16, 17)),
        new(true, "final doji", 0, (19, 20, 18, 19)),
        new(true, "wrong final color", 0, (19, 22, 18, 21)),
        new(true, "close just below second open", 1, (19, 20, 16, 16.875)),
        new(false, "ordinary gap", 1, (11, 16, 10, 15)),
        new(false, "open outside second body", 1, (0, 16, 0, 15)),
        new(false, "close equals second high", 0, (11, 16, 10, 14)),
        new(false, "close equals first low", 0, (11, 16, 10, 16)),
        new(false, "final doji", 0, (15, 16, 14, 15)),
        new(false, "wrong final color", 0, (16, 17, 14, 15)),
        new(false, "close just above second high", 1, (11, 16, 10, 14.125)),
        new(false, "close just below first low", 1, (11, 16, 10, 15.875)),
    ];

    internal static CompetitorData Golden(Case item, int period)
    {
        var a = item.Upside ? (O: 10d, H: 14d, L: 9d, C: 13d) : (O: 21d, H: 22d, L: 16d, C: 17d);
        var bars = new List<(double O, double H, double L, double C)>();
        for (var j = period - 1; j >= 0; j--)
        {
            var offset = (item.Upside ? -2d : 2d) * j;
            bars.Add((a.O + offset, a.H + offset, a.L + offset, a.C + offset));
        }
        bars.Add(item.Upside ? (17, 22, 16, 21) : (13, 14, 9, 10));
        bars.Add(item.Final);
        return CompetitorData.FromOhlc(
            bars.Select(b => b.O).ToArray(),
            bars.Select(b => b.H).ToArray(),
            bars.Select(b => b.L).ToArray(),
            bars.Select(b => b.C).ToArray()
        );
    }

    internal static CompetitorData Fixture(int period)
    {
        var bars = Cases.SelectMany(c => Golden(c, period).IndicatorBars).ToArray();
        return CompetitorData.FromOhlc(
            bars.Select(b => b.Open).ToArray(),
            bars.Select(b => b.High).ToArray(),
            bars.Select(b => b.Low).ToArray(),
            bars.Select(b => b.Close).ToArray()
        );
    }
}

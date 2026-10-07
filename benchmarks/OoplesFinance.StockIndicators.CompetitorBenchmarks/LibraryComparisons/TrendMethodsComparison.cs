using OoplesFinance.StockIndicators.Indicators;
using TC = Trady.Analysis.Candlestick;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class TrendMethodsComparison
{
    internal static readonly ComparisonPair[] Pairs = [Pair(true), Pair(false)];

    internal static IIndicator Create(
        bool rising,
        int trend = 3,
        int period = 20,
        decimal shortQ = .25m,
        decimal longQ = .75m
    ) =>
        rising
            ? new RisingThreeMethodsPattern(trend, period, shortQ, longQ)
            : new FallingThreeMethodsPattern(trend, period, shortQ);

    internal static ComparisonPair Pair(
        bool rising,
        int period = 20,
        decimal shortQ = .25m,
        decimal longQ = .75m
    ) =>
        new(
            "Trady.Candlestick." + (rising ? "Rising" : "Falling") + "ThreeMethods",
            rising ? "RisingThreeMethodsPattern" : "FallingThreeMethodsPattern",
            (data, trend) => Competitor(data, rising, trend, period, shortQ, longQ),
            (data, trend) =>
                CandleComparisonExecution.Run(
                    Create(rising, trend, period, shortQ, longQ),
                    data,
                    Math.Min(1, data.Count)
                ),
            (data, trend) => Reference(data, rising, trend, period, shortQ, longQ)
        );

    private static ComparisonSeries Competitor(
        CompetitorData data,
        bool rising,
        int trend,
        int period,
        decimal shortQ,
        decimal longQ
    )
    {
        var ticks = rising
            ? new TC.RisingThreeMethods(data.Candles, trend, period, shortQ, longQ).Compute()
            : new TC.FallingThreeMethods(data.Candles, trend, period, shortQ, longQ).Compute();
        return new(
            Math.Min(1, data.Count),
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
        bool rising,
        int trend,
        int period,
        decimal shortQ,
        decimal longQ
    )
    {
        var bodies = data
            .Opens.Select((v, i) => Math.Abs((decimal)v - (decimal)data.Closes[i]))
            .ToArray();
        bool Rank(int i, bool longBody)
        {
            var p = longBody && !rising ? 20 : period;
            var q = longBody ? (rising ? longQ : .75m) : shortQ;
            if (i < p - 1)
                return false;
            var w = bodies.Skip(i - p + 1).Take(p).Order().ToArray();
            var rank = q * (p - 1);
            var lo = (int)rank;
            var threshold = w[lo];
            if (lo + 1 < p)
                threshold += (w[lo + 1] - threshold) * (rank - lo);
            return longBody
                ? bodies[i] >= threshold
                    && (rising ? data.Closes[i] > data.Opens[i] : data.Closes[i] < data.Opens[i])
                : bodies[i] < threshold;
        }
        var longs = Enumerable.Range(0, data.Count).Select(i => Rank(i, true)).ToArray();
        var shorts = Enumerable.Range(0, data.Count).Select(i => Rank(i, false)).ToArray();
        var output = new double[data.Count];
        for (var i = 1; i < data.Count; i++)
        {
            if (!longs[i] || !shorts[i - 1])
                continue;
            for (var j = i - 1; j >= trend; j--)
            {
                var step = rising
                    ? data.Opens[j] < data.Opens[j - 1] && data.Closes[j] < data.Closes[j - 1]
                    : data.Opens[j] > data.Opens[j - 1] && data.Closes[j] > data.Closes[j - 1];
                if (shorts[j] && !longs[j - 1] && !step)
                    break;
                if (!longs[j])
                    continue;
                var trending = Enumerable
                    .Range(j - trend + 1, trend)
                    .All(k =>
                        rising
                            ? data.Highs[k] > data.Highs[k - 1] && data.Lows[k] > data.Lows[k - 1]
                            : data.Highs[k] < data.Highs[k - 1] && data.Lows[k] < data.Lows[k - 1]
                    );
                var ends = rising
                    ? data.Highs[i] > data.Highs[j]
                        && data.Highs[j] > data.Highs[j + 1]
                        && data.Lows[j] < data.Lows[i - 1]
                    : data.Lows[i] < data.Lows[j]
                        && data.Lows[j] < data.Lows[j + 1]
                        && data.Highs[j] > data.Highs[i - 1];
                output[i] = trending && ends ? 1 : 0;
                break;
            }
        }
        return new(Math.Min(1, data.Count), output);
    }

    internal sealed record Case(
        string Name,
        double Expected,
        (double O, double H, double L, double C)[] Tail
    );

    private static readonly (double O, double H, double L, double C) Anchor = (50, 62, 49, 60),
        Reaction1 = (58, 61, 52, 58.5),
        Reaction2 = (57, 60, 53, 57.5),
        Final = (58, 65, 54, 64);
    internal static readonly Case[] Cases =
    [
        new(
            "newest anchor fails trend",
            0,
            [Anchor, Reaction1, (52, 61, 50, 60), (57, 60, 51, 57.5), Final]
        ),
        Alter("wrong anchor color", 0, (60, 62, 49, 50)),
        new("two reactions", 1, [Anchor, Reaction1, Reaction2, Final]),
        new("one reaction", 1, [Anchor, Reaction1, Final]),
        new("three reactions", 1, [Anchor, Reaction1, Reaction2, (56, 59, 54, 56.5), Final]),
        Alter("equal final high", 3, (54, 62, 53, 62)),
        Alter("equal first reaction high", 1, (58, 62, 52, 58.5)),
        Alter("equal final reaction low", 2, (57, 60, 49, 57.5)),
        Alter("equal reaction open", 2, (58, 60, 53, 58.25)),
        Alter("equal reaction close", 2, (58.25, 60, 53, 58.5)),
        Alter("wrong final color", 3, (64, 65, 54, 58)),
        new(
            "long bearish reaction ignored",
            1,
            [Anchor, Reaction1, (57, 60, 51, 52), (51, 58, 50, 51.5), Final]
        ),
        Alter("final body too short", 3, (58, 65, 54, 58.5)),
        Alter("reaction not short", 2, (57, 60, 53, 54)),
    ];

    private static Case Alter(
        string name,
        int index,
        (double O, double H, double L, double C) candle
    )
    {
        (double O, double H, double L, double C)[] tail = [Anchor, Reaction1, Reaction2, Final];
        tail[index] = candle;
        return new(name, 0, tail);
    }

    internal static CompetitorData Golden(Case item, bool rising, int trend = 3, int period = 20)
    {
        var bars = new List<(double O, double H, double L, double C)>();
        var count = Math.Max(Math.Max(period, 20), trend);
        for (var i = count - 1; i >= 0; i--)
            bars.Add((48 - 2d * i, 50 - 2d * i, 47 - 2d * i, 49 - 2d * i));
        bars.AddRange(item.Tail);
        if (!rising)
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
                new[] { true, false }.SelectMany(r => Golden(c, r, trend).IndicatorBars)
            )
            .ToArray();
        return CrowSoldierComparison.FromBars(bars);
    }
}

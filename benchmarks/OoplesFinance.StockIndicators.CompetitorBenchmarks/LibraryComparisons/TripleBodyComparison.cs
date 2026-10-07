using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class TripleBodyComparison
{
    internal static readonly string[] Names =
    [
        "ThreeInside",
        "TwoCrows",
        "UpsideGapTwoCrows",
        "UniqueThreeRiver",
    ];
    internal static readonly ComparisonPair[] Pairs = Names
        .Select(n => new ComparisonPair(
            "TaLib.Candles." + n,
            n + "Candle",
            (d, _) => Competitor(n, d),
            (d, _) => Ooples(n, d),
            (d, _) => Reference(n, d)
        ))
        .ToArray();

    internal static IIndicator Create(string name, int longPeriod = 10, int shortPeriod = 10) =>
        name switch
        {
            "ThreeInside" => new ThreeInsideCandle(longPeriod, shortPeriod),
            "TwoCrows" => new TwoCrowsCandle(longPeriod),
            "UpsideGapTwoCrows" => new UpsideGapTwoCrowsCandle(longPeriod, shortPeriod),
            "UniqueThreeRiver" => new UniqueThreeRiverCandle(longPeriod, shortPeriod),
            _ => throw new ArgumentException("Unknown triple-body pattern", nameof(name)),
        };

    private static ComparisonSeries Ooples(string name, CompetitorData data)
    {
        return CandleComparisonExecution.Run(Create(name), data, Math.Min(12, data.Count));
    }

    private static ComparisonSeries Competitor(string name, CompetitorData d)
    {
        var packed = new int[d.Count];
        System.Range range;
        var code = name switch
        {
            "ThreeInside" => Candles.ThreeInside<double>(
                d.Opens,
                d.Highs,
                d.Lows,
                d.Closes,
                System.Range.All,
                packed,
                out range
            ),
            "TwoCrows" => Candles.TwoCrows<double>(
                d.Opens,
                d.Highs,
                d.Lows,
                d.Closes,
                System.Range.All,
                packed,
                out range
            ),
            "UpsideGapTwoCrows" => Candles.UpsideGapTwoCrows<double>(
                d.Opens,
                d.Highs,
                d.Lows,
                d.Closes,
                System.Range.All,
                packed,
                out range
            ),
            _ => Candles.UniqueThreeRiver<double>(
                d.Opens,
                d.Highs,
                d.Lows,
                d.Closes,
                System.Range.All,
                packed,
                out range
            ),
        };
        return CandleComparisonExecution.Unpack(
            code,
            packed,
            range,
            d.Count,
            Math.Min(12, d.Count),
            name
        );
    }

    private static ComparisonSeries Reference(string name, CompetitorData d)
    {
        var values = new double[d.Count];
        decimal Body(int j) => Math.Abs((decimal)d.Opens[j] - (decimal)d.Closes[j]);
        decimal Mean(int end) => Enumerable.Range(end - 10, 10).Sum(Body) / 10;
        for (var i = 12; i < d.Count; i++)
        {
            var a = i - 2;
            var b = i - 1;
            if (Body(a) <= Mean(a))
                continue;
            if (name is "ThreeInside" or "UpsideGapTwoCrows" && Body(b) > Mean(b))
                continue;
            if (name == "UniqueThreeRiver" && Body(i) >= Mean(i))
                continue;
            var ao = d.Opens[a];
            var ac = d.Closes[a];
            var bo = d.Opens[b];
            var bc = d.Closes[b];
            var co = d.Opens[i];
            var cc = d.Closes[i];
            var match = name switch
            {
                "ThreeInside" => Math.Min(bo, bc) > Math.Min(ao, ac)
                    && Math.Max(bo, bc) < Math.Max(ao, ac)
                    && (ac >= ao ? cc < co && cc < ao : cc >= co && cc > ao),
                "TwoCrows" => ac >= ao
                    && bc < bo
                    && bc > ac
                    && cc < co
                    && co > bc
                    && co < bo
                    && cc > ao
                    && cc < ac,
                "UpsideGapTwoCrows" => ac >= ao
                    && bc < bo
                    && bc > ac
                    && cc < co
                    && co > bo
                    && cc < bc
                    && cc > ac,
                _ => ac < ao
                    && bc < bo
                    && bc > ac
                    && bo <= ao
                    && d.Lows[b] < d.Lows[a]
                    && cc >= co
                    && co > d.Lows[b],
            };
            if (match)
                values[i] =
                    name == "UniqueThreeRiver" || name == "ThreeInside" && ac < ao ? 100 : -100;
        }
        return new(Math.Min(12, d.Count), values);
    }

    internal sealed record Golden(
        string Name,
        double AO,
        double AC,
        double BO,
        double BC,
        double BL,
        double CO,
        double CC,
        double Expected
    );

    internal static readonly Golden[] Cases =
    [
        new("ThreeInside", 2, 9, 4, 6.5, 0, 8, 1, -100),
        new("ThreeInside", 9, 2, 6.5, 4, 0, 1, 10, 100),
        new("ThreeInside", 2, 9, 4, 6.625, 0, 8, 1, 0),
        new("ThreeInside", 2, 9, 2, 4, 0, 8, 1, 0),
        new("ThreeInside", 2, 9, 7, 9, 0, 8, 1, 0),
        new("ThreeInside", 2, 9, 4, 6, 0, 8, 2, 0),
        new("ThreeInside", 2, 9, 4, 6, 0, 0, 1, 0),
        new("ThreeInside", 9, 2, 4, 4, 0, 10, 10, 100),
        new("TwoCrows", 2, 9, 12, 10, 0, 11, 5, -100),
        new("TwoCrows", 2, 9, 12, 10, 0, 12, 5, 0),
        new("TwoCrows", 2, 9, 12, 10, 0, 10, 5, 0),
        new("TwoCrows", 2, 9, 12, 10, 0, 11, 2, 0),
        new("TwoCrows", 2, 9, 12, 10, 0, 11, 9, 0),
        new("TwoCrows", 2, 9, 11, 9, 0, 10, 5, 0),
        new("TwoCrows", 2, 9, 20, 10, 0, 15, 5, -100),
        new("UpsideGapTwoCrows", 2, 9, 12, 10, 0, 13, 9.5, -100),
        new("UpsideGapTwoCrows", 2, 9, 12, 10, 0, 12, 9.5, 0),
        new("UpsideGapTwoCrows", 2, 9, 12, 10, 0, 13, 10, 0),
        new("UpsideGapTwoCrows", 2, 9, 12, 10, 0, 13, 9, 0),
        new("UpsideGapTwoCrows", 2, 9, 11, 9, 0, 12, 8.5, 0),
        new("UpsideGapTwoCrows", 2, 9, 12.5, 10, 0, 13, 9.5, -100),
        new("UpsideGapTwoCrows", 2, 9, 12.625, 10, 0, 13, 9.5, 0),
        new("UniqueThreeRiver", 9, 2, 6, 4, -1, 0, 1, 100),
        new("UniqueThreeRiver", 9, 2, 9, 4, -1, 0, 1, 100),
        new("UniqueThreeRiver", 9, 2, 9.125, 4, -1, 0, 1, 0),
        new("UniqueThreeRiver", 9, 2, 6, 2, -1, 0, 1, 0),
        new("UniqueThreeRiver", 9, 2, 6, 4, 0, 1, 2, 0),
        new("UniqueThreeRiver", 9, 2, 6, 4, -1, -1, 0, 0),
        new("UniqueThreeRiver", 9, 2, 6, 4, -1, 0, 2.5, 0),
        new("UniqueThreeRiver", 9, 2, 6, 4, -1, 0, 2.375, 100),
        new("UniqueThreeRiver", 9, 2, 6, 4, -1, 0, 0, 100),
    ];

    internal static CompetitorData Fixture()
    {
        var bars = Cases.SelectMany(c => BarsFor(c)).ToArray();
        return CompetitorData.FromOhlc(
            bars.Select(b => b.Open).ToArray(),
            bars.Select(b => b.High).ToArray(),
            bars.Select(b => b.Low).ToArray(),
            bars.Select(b => b.Close).ToArray()
        );
    }

    internal static Bar[] BarsFor(Golden c)
    {
        var bars = Enumerable
            .Range(0, 10)
            .Select(i => new Bar(DateTime.UnixEpoch.AddDays(i), 4, 30, 0, 6, 1))
            .ToList();
        bars.Add(new(DateTime.UnixEpoch.AddDays(10), c.AO, 30, 0, c.AC, 1));
        bars.Add(new(DateTime.UnixEpoch.AddDays(11), c.BO, 30, c.BL, c.BC, 1));
        bars.Add(
            new(
                DateTime.UnixEpoch.AddDays(12),
                c.CO,
                30,
                Math.Min(-2, Math.Min(c.CO, c.CC)),
                c.CC,
                1
            )
        );
        return bars.ToArray();
    }
}

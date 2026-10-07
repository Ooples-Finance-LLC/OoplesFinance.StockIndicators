using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class StarReversalComparison
{
    internal static readonly string[] Names =
    [
        "MorningStar",
        "EveningStar",
        "MorningDojiStar",
        "EveningDojiStar",
    ];
    internal static readonly ComparisonPair[] Pairs = Names
        .Select(n => new ComparisonPair(
            "TaLib.Candles." + n,
            n + "Candle",
            (d, _) => Competitor(n, d),
            (d, _) => Ooples(n, d),
            (d, _) => Reference(n, d),
            CompetitorReference: (d, _) => Reference(n, d, competitorArithmetic: true)
        ))
        .ToArray();

    internal static IIndicator Create(
        string name,
        int longPeriod = 10,
        int shortPeriod = 10,
        int dojiPeriod = 10,
        double penetration = .3
    ) =>
        name switch
        {
            "MorningStar" => new MorningStarCandle(longPeriod, shortPeriod, penetration),
            "EveningStar" => new EveningStarCandle(longPeriod, shortPeriod, penetration),
            "MorningDojiStar" => new MorningDojiStarCandle(
                longPeriod,
                shortPeriod,
                dojiPeriod,
                penetration
            ),
            _ => new EveningDojiStarCandle(longPeriod, shortPeriod, dojiPeriod, penetration),
        };

    internal static ComparisonSeries Ooples(string name, CompetitorData d, double penetration = .3)
    {
        return CandleComparisonExecution.Run(
            Create(name, penetration: penetration),
            d,
            Math.Min(12, d.Count)
        );
    }

    internal static ComparisonSeries Competitor(
        string name,
        CompetitorData d,
        double penetration = .3
    )
    {
        var packed = new int[d.Count];
        System.Range range;
        var code = name switch
        {
            "MorningStar" => Candles.MorningStar<double>(
                d.Opens,
                d.Highs,
                d.Lows,
                d.Closes,
                System.Range.All,
                packed,
                out range,
                penetration
            ),
            "EveningStar" => Candles.EveningStar<double>(
                d.Opens,
                d.Highs,
                d.Lows,
                d.Closes,
                System.Range.All,
                packed,
                out range,
                penetration
            ),
            "MorningDojiStar" => Candles.MorningDojiStar<double>(
                d.Opens,
                d.Highs,
                d.Lows,
                d.Closes,
                System.Range.All,
                packed,
                out range,
                penetration
            ),
            _ => Candles.EveningDojiStar<double>(
                d.Opens,
                d.Highs,
                d.Lows,
                d.Closes,
                System.Range.All,
                packed,
                out range,
                penetration
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

    internal static ComparisonSeries Reference(
        string name,
        CompetitorData d,
        double penetration = .3,
        bool competitorArithmetic = false
    )
    {
        var values = new double[d.Count];
        var morning = name.StartsWith("Morning", StringComparison.Ordinal);
        var doji = name.Contains("Doji", StringComparison.Ordinal);
        decimal Body(int i) => Math.Abs((decimal)d.Closes[i] - (decimal)d.Opens[i]);
        decimal Mean(int end, bool range) =>
            Enumerable
                .Range(end - 10, 10)
                .Sum(j => range ? (decimal)d.Highs[j] - (decimal)d.Lows[j] : Body(j)) / 10;
        for (var i = 12; i < d.Count; i++)
        {
            var a = i - 2;
            var b = i - 1;
            if (
                morning
                    ? d.Closes[a] >= d.Opens[a]
                        || d.Closes[i] < d.Opens[i]
                        || Math.Max(d.Opens[b], d.Closes[b]) >= d.Closes[a]
                    : d.Closes[a] < d.Opens[a]
                        || d.Closes[i] >= d.Opens[i]
                        || Math.Min(d.Opens[b], d.Closes[b]) <= d.Closes[a]
            )
                continue;
            if (
                Body(a) <= Mean(a, false)
                || Body(i) <= Mean(i, false)
                || Body(b) > (doji ? Mean(b, true) / 10 : Mean(b, false))
            )
                continue;
            if (
                PenetrationReferenceArithmetic.Passes(
                    d.Opens[a],
                    d.Closes[a],
                    d.Closes[i],
                    penetration,
                    morning,
                    competitorArithmetic
                )
            )
                values[i] = morning ? 100 : -100;
        }
        return new(Math.Min(12, d.Count), values);
    }

    internal sealed record Golden(string Name, Bar[] Bars, double Penetration, double Expected);

    private static (double O, double C) B(double o, double c) => (o, c);

    private static Golden Case(
        string name,
        double expected,
        double penetration,
        params (double O, double C)[] pattern
    )
    {
        var bars = Enumerable
            .Range(0, 10)
            .Select(i => new Bar(DateTime.UnixEpoch.AddDays(i), 4, 10, 0, 6, 1))
            .ToList();
        foreach (var b in pattern)
        {
            var low = Math.Min(b.O, b.C) - .5;
            bars.Add(new(DateTime.UnixEpoch.AddDays(bars.Count), b.O, low + 10, low, b.C, 1));
        }
        return new(name, bars.ToArray(), penetration, expected);
    }

    private static Golden Mirror(Golden c) =>
        new(
            c.Name.Replace("Morning", "Evening", StringComparison.Ordinal),
            c.Bars.Select(b => new Bar(b.Time, -b.Open, -b.Low, -b.High, -b.Close, 1)).ToArray(),
            c.Penetration,
            -c.Expected
        );

    internal static readonly Golden[] Cases = BuildCases();

    private static Golden[] BuildCases()
    {
        var cases = new List<Golden>();
        foreach (var n in new[] { "MorningStar", "MorningDojiStar" })
        {
            cases.AddRange([
                Case(n, 100, .5, B(8, 2), B(1, 0), B(-1, 6)),
                Case(n, 0, .5, B(8, 2), B(1, 0), B(-1, 5)),
                Case(n, 100, .5, B(8, 2), B(1, 0), B(-1, 5.125)),
                Case(n, 100, .5, B(8, 2), B(0, 1), B(-1, 6)),
                Case(n, 100, .5, B(8, 2), B(1, 1), B(-1, 6)),
                Case(n, 0, .5, B(8, 2), B(2, 1), B(-1, 6)),
                Case(n, 0, .5, B(8, 2), B(1, 2), B(-1, 6)),
                Case(n, 0, .5, B(4, 2), B(1, 0), B(-1, 6)),
                Case(n, 0, .5, B(2, 8), B(1, 0), B(-1, 6)),
                Case(n, 0, .5, B(8, 2), B(1, 0), B(6, -1)),
                Case(n, 0, 0, B(5, 2), B(1, 0), B(1, 3)),
                Case(n, 100, 0, B(5, 2), B(1, 0), B(1, 3.125)),
                Case(n, 100, 1, B(8, 2), B(1, 0), B(7, 10)),
                Case(n, 100, 2, B(4, 1), B(0, -1), B(4, 8)),
                Case(n, 0, 2, B(4, 1), B(0, -1), B(4, 7)),
            ]);
        }
        cases.Add(Case("MorningStar", 100, .5, B(9, 2), B(1, -1.5), B(-2, 7)));
        cases.Add(Case("MorningStar", 0, .5, B(9, 2), B(1, -1.625), B(-2, 7)));
        cases.Add(Case("MorningDojiStar", 0, .5, B(8, 2), B(1, -.125), B(-1, 6)));
        return cases.Concat(cases.Select(Mirror)).ToArray();
    }

    internal static CompetitorData Fixture() =>
        CrowSoldierComparison.FromBars(Cases.SelectMany(c => c.Bars).ToArray());
}

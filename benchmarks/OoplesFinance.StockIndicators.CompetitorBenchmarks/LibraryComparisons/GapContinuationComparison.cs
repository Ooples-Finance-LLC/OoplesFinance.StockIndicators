using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class GapContinuationComparison
{
    internal static readonly string[] Names =
    [
        "AbandonedBaby",
        "Tristar",
        "TasukiGap",
        "UpDownSideGapThreeMethods",
    ];
    internal static readonly ComparisonPair[] Pairs = Names
        .Select(n => new ComparisonPair(
            "TaLib.Candles." + n,
            n + "Candle",
            (d, _) => Competitor(n, d),
            (d, _) => Ooples(n, d),
            (d, _) => Reference(n, d),
            CompetitorReference: n == "AbandonedBaby"
                ? (d, _) => Reference(n, d, competitorArithmetic: true)
                : null
        ))
        .ToArray();

    internal static IIndicator Create(
        string name,
        int period = 10,
        int shortPeriod = 10,
        int dojiPeriod = 10,
        double penetration = .3
    ) =>
        name switch
        {
            "AbandonedBaby" => new AbandonedBabyCandle(
                period,
                shortPeriod,
                dojiPeriod,
                penetration
            ),
            "Tristar" => new TristarCandle(period),
            "TasukiGap" => new TasukiGapCandle(period),
            _ => new UpDownSideGapThreeMethodsCandle(),
        };

    private static int First(string name, int count) =>
        Math.Min(
            name == "UpDownSideGapThreeMethods" ? 2
                : name == "TasukiGap" ? 7
                : 12,
            count
        );

    internal static ComparisonSeries Ooples(string name, CompetitorData d, double penetration = .3)
    {
        return CandleComparisonExecution.Run(
            Create(name, name == "TasukiGap" ? 5 : 10, penetration: penetration),
            d,
            First(name, d.Count)
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
            "AbandonedBaby" => Candles.AbandonedBaby<double>(
                d.Opens,
                d.Highs,
                d.Lows,
                d.Closes,
                System.Range.All,
                packed,
                out range,
                penetration
            ),
            "Tristar" => Candles.Tristar<double>(
                d.Opens,
                d.Highs,
                d.Lows,
                d.Closes,
                System.Range.All,
                packed,
                out range
            ),
            "TasukiGap" => Candles.TasukiGap<double>(
                d.Opens,
                d.Highs,
                d.Lows,
                d.Closes,
                System.Range.All,
                packed,
                out range
            ),
            _ => Candles.UpDownSideGapThreeMethods<double>(
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
            First(name, d.Count),
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
        decimal Body(int i) => Math.Abs((decimal)d.Closes[i] - (decimal)d.Opens[i]);
        decimal Range(int i) => (decimal)d.Highs[i] - (decimal)d.Lows[i];
        decimal Mean(int end, int period, Func<int, decimal> term) =>
            Enumerable.Range(end - period, period).Sum(term) / period;
        for (var i = First(name, d.Count); i < d.Count; i++)
        {
            var a = i - 2;
            var b = i - 1;
            var up = Math.Min(d.Opens[b], d.Closes[b]) > Math.Max(d.Opens[a], d.Closes[a]);
            var down = Math.Max(d.Opens[b], d.Closes[b]) < Math.Min(d.Opens[a], d.Closes[a]);
            if (name == "AbandonedBaby")
            {
                var morning = d.Closes[a] < d.Opens[a];
                if (
                    Body(a) <= Mean(a, 10, Body)
                    || Body(b) > Mean(b, 10, Range) / 10
                    || Body(i) <= Mean(i, 10, Body)
                )
                    continue;
                if (
                    morning
                        ? d.Closes[i] < d.Opens[i]
                            || d.Highs[b] >= d.Lows[a]
                            || d.Highs[b] >= d.Lows[i]
                        : d.Closes[i] >= d.Opens[i]
                            || d.Lows[b] <= d.Highs[a]
                            || d.Lows[b] <= d.Highs[i]
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
                continue;
            }
            if (name == "Tristar")
            {
                var threshold = Mean(a, 10, Range) / 10;
                if (Body(a) > threshold || Body(b) > threshold || Body(i) > threshold)
                    continue;
                if (up && Math.Max(d.Opens[i], d.Closes[i]) < Math.Max(d.Opens[b], d.Closes[b]))
                    values[i] = -100;
                else if (
                    down
                    && Math.Min(d.Opens[i], d.Closes[i]) > Math.Min(d.Opens[b], d.Closes[b])
                )
                    values[i] = 100;
                continue;
            }
            var white = d.Closes[b] >= d.Opens[b];
            if (
                white == (d.Closes[i] >= d.Opens[i])
                || d.Opens[i] <= Math.Min(d.Opens[b], d.Closes[b])
                || d.Opens[i] >= Math.Max(d.Opens[b], d.Closes[b])
            )
                continue;
            bool match;
            if (name == "UpDownSideGapThreeMethods")
                match =
                    white == (d.Closes[a] >= d.Opens[a])
                    && (white ? up : down)
                    && d.Closes[i] > Math.Min(d.Opens[a], d.Closes[a])
                    && d.Closes[i] < Math.Max(d.Opens[a], d.Closes[a]);
            else
                match =
                    (
                        white
                            ? up
                                && d.Closes[i] < d.Opens[b]
                                && d.Closes[i] > Math.Max(d.Opens[a], d.Closes[a])
                            : down
                                && d.Closes[i] > d.Opens[b]
                                && d.Closes[i] < Math.Min(d.Opens[a], d.Closes[a])
                    )
                    && Math.Abs(Body(b) - Body(i)) < Mean(b, 5, Range) / 5;
            if (match)
                values[i] = white ? 100 : -100;
        }
        return new(First(name, d.Count), values);
    }

    internal sealed record Golden(string Name, Bar[] Bars, double Penetration, double Expected);

    private static (double O, double H, double L, double C) B(double o, double c) =>
        (o, Math.Min(o, c) + 10, Math.Min(o, c), c);

    private static Golden Case(
        string name,
        double expected,
        params (double O, double H, double L, double C)[] pattern
    )
    {
        var bars = Enumerable
            .Range(0, 10)
            .Select(i => new Bar(DateTime.UnixEpoch.AddDays(i), 4, 10, 0, 6, 1))
            .ToList();
        foreach (var b in pattern)
            bars.Add(new(DateTime.UnixEpoch.AddDays(bars.Count), b.O, b.H, b.L, b.C, 1));
        return new(name, bars.ToArray(), .5, expected);
    }

    private static Golden Mirror(Golden c) =>
        new(
            c.Name,
            c.Bars.Select(b => new Bar(b.Time, -b.Open, -b.Low, -b.High, -b.Close, 1)).ToArray(),
            c.Penetration,
            -c.Expected
        );

    internal static readonly Golden[] Cases = BuildCases();

    private static Golden[] BuildCases()
    {
        var cases = new List<Golden>
        {
            Case("AbandonedBaby", 100, (8, 10, 2, 2), (0, 1, -1, 0), (2, 8, 2, 6)),
            Case("AbandonedBaby", 0, (8, 10, 2, 2), (0, 2, -1, 0), (2, 8, 2, 6)),
            Case("AbandonedBaby", 0, (8, 10, 2, 2), (0, 1, -1, 0), (2, 8, 1, 6)),
            Case("AbandonedBaby", 0, (8, 10, 2, 2), (0, 1, -1, 0), (2, 8, 2, 5)),
            Case("AbandonedBaby", 100, (8, 10, 2, 2), (0, 1, -1, 0), (2, 8, 2, 5.125)),
            Case("AbandonedBaby", 100, (8, 12, 2, 2), (0, 1, -1, 1), (3, 9, 3, 7)),
            Case("AbandonedBaby", 0, (8, 12, 2, 2), (0, 1.125, -1, 1.125), (3, 9, 3, 7)),
            Case("AbandonedBaby", 0, (4, 12, 2, 2), (0, 1, -1, 0), (2, 8, 2, 6)),
            Case("AbandonedBaby", 0, (5, 12, 2, 2), (0, 1, -1, 1), (3, 8, 3, 5)),
            Case("AbandonedBaby", 100, (5, 12, 2, 2), (0, 1, -1, 1), (3, 8, 3, 5.125)),
            Case("Tristar", -100, B(1, 2), B(4, 5), B(3, 4)),
            Case("Tristar", -100, B(1, 1), B(4, 4), B(3, 3)),
            Case("Tristar", 0, B(1, 2.125), B(4, 5), B(3, 4)),
            Case("Tristar", 0, B(1, 2), B(4, 5.125), B(3, 4)),
            Case("Tristar", 0, B(1, 2), B(4, 5), B(3, 4.125)),
            Case("Tristar", 0, B(1, 2), B(2, 3), B(1, 2)),
            Case("Tristar", 0, B(1, 2), B(4, 5), B(4, 5)),
            Case("Tristar", -100, B(1, 2), B(4, 5), B(4, 4.5)),
            Case("Tristar", 0, (1, 101, 1, 2), B(4, 5), B(3, 4.125)),
            Case("TasukiGap", 100, B(0, 2), B(4, 8), B(7, 3)),
            Case("TasukiGap", 100, B(2, 0), B(4, 8), B(7, 3)),
            Case("TasukiGap", 0, B(0, 2), B(4, 8), B(8, 3)),
            Case("TasukiGap", 0, B(0, 2), B(4, 8), B(4, 3)),
            Case("TasukiGap", 0, B(0, 2), B(4, 8), B(7, 2)),
            Case("TasukiGap", 0, B(0, 2), B(4, 8), B(7, 4)),
            Case("TasukiGap", 0, B(0, 4), B(4, 8), B(7, 3)),
            Case("TasukiGap", 0, B(0, 2), B(4, 8), B(5, 3)),
            Case("TasukiGap", 0, B(0, 2), (4, 104, 4, 8), B(5, 3)),
            Case("TasukiGap", 100, B(0, 2), B(4, 8), B(5.125, 3)),
            Case("TasukiGap", 0, B(0, 2), B(8, 4), B(7, 3)),
            Case("UpDownSideGapThreeMethods", 100, B(0, 2), B(4, 8), B(7, 1)),
            Case("UpDownSideGapThreeMethods", 0, B(2, 0), B(4, 8), B(7, 1)),
            Case("UpDownSideGapThreeMethods", 0, B(0, 2), B(8, 4), B(7, 1)),
            Case("UpDownSideGapThreeMethods", 0, B(0, 2), B(4, 8), B(8, 1)),
            Case("UpDownSideGapThreeMethods", 0, B(0, 2), B(4, 8), B(4, 1)),
            Case("UpDownSideGapThreeMethods", 0, B(0, 2), B(4, 8), B(7, 0)),
            Case("UpDownSideGapThreeMethods", 0, B(0, 2), B(4, 8), B(7, 2)),
            Case("UpDownSideGapThreeMethods", 0, B(0, 4), B(4, 8), B(7, 1)),
        };
        return cases.Concat(cases.Select(Mirror)).ToArray();
    }

    internal static CompetitorData Fixture() =>
        CrowSoldierComparison.FromBars(Cases.SelectMany(c => c.Bars).ToArray());
}

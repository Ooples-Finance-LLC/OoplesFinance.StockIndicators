using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class CrowSoldierComparison
{
    internal static readonly string[] Names =
    [
        "ThreeBlackCrows",
        "IdenticalThreeCrows",
        "ThreeWhiteSoldiers",
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

    internal static IIndicator Create(
        string name,
        int shadow = 10,
        int near = 5,
        int far = 5,
        int body = 10
    ) =>
        name switch
        {
            "ThreeBlackCrows" => new ThreeBlackCrowsCandle(shadow),
            "IdenticalThreeCrows" => new IdenticalThreeCrowsCandle(shadow, near),
            _ => new ThreeWhiteSoldiersCandle(shadow, near, far, body),
        };

    private static int First(string name, int count) =>
        Math.Min(name == "ThreeBlackCrows" ? 13 : 12, count);

    private static ComparisonSeries Ooples(string name, CompetitorData d)
    {
        return CandleComparisonExecution.Run(Create(name), d, First(name, d.Count));
    }

    private static ComparisonSeries Competitor(string name, CompetitorData d)
    {
        var packed = new int[d.Count];
        System.Range range;
        var code = name switch
        {
            "ThreeBlackCrows" => Candles.ThreeBlackCrows<double>(
                d.Opens,
                d.Highs,
                d.Lows,
                d.Closes,
                System.Range.All,
                packed,
                out range
            ),
            "IdenticalThreeCrows" => Candles.IdenticalThreeCrows<double>(
                d.Opens,
                d.Highs,
                d.Lows,
                d.Closes,
                System.Range.All,
                packed,
                out range
            ),
            _ => Candles.ThreeWhiteSoldiers<double>(
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

    private static ComparisonSeries Reference(string name, CompetitorData d)
    {
        var values = new double[d.Count];
        var white = name == "ThreeWhiteSoldiers";
        decimal Body(int i) => Math.Abs((decimal)d.Closes[i] - (decimal)d.Opens[i]);
        decimal Range(int i) => (decimal)d.Highs[i] - (decimal)d.Lows[i];
        decimal Sum(int end, int period, Func<int, decimal> term) =>
            Enumerable.Range(end - period, period).Sum(term);
        for (var i = First(name, d.Count); i < d.Count; i++)
        {
            var a = i - 2;
            var b = i - 1;
            if (
                Enumerable
                    .Range(a, 3)
                    .Any(j => white ? d.Closes[j] < d.Opens[j] : d.Closes[j] >= d.Opens[j])
            )
                continue;
            if (
                white
                    ? d.Closes[b] <= d.Closes[a] || d.Closes[i] <= d.Closes[b]
                    : d.Closes[b] >= d.Closes[a] || d.Closes[i] >= d.Closes[b]
            )
                continue;
            if (
                Enumerable
                    .Range(a, 3)
                    .Any(j =>
                        (
                            white
                                ? (decimal)d.Highs[j] - (decimal)d.Closes[j]
                                : (decimal)d.Closes[j] - (decimal)d.Lows[j]
                        )
                        >= Sum(j, 10, Range) / 100
                    )
            )
                continue;
            bool match;
            if (name == "ThreeBlackCrows")
                match =
                    d.Closes[i - 3] >= d.Opens[i - 3]
                    && d.Highs[i - 3] > d.Closes[a]
                    && d.Opens[b] > d.Closes[a]
                    && d.Opens[b] < d.Opens[a]
                    && d.Opens[i] > d.Closes[b]
                    && d.Opens[i] < d.Opens[b];
            else if (!white)
                match =
                    Math.Abs((decimal)d.Opens[b] - (decimal)d.Closes[a]) <= Sum(a, 5, Range) / 100
                    && Math.Abs((decimal)d.Opens[i] - (decimal)d.Closes[b])
                        <= Sum(b, 5, Range) / 100;
            else
                match =
                    d.Opens[b] > d.Opens[a]
                    && d.Opens[i] > d.Opens[b]
                    && (decimal)d.Opens[b] <= (decimal)d.Closes[a] + Sum(a, 5, Range) / 25
                    && (decimal)d.Opens[i] <= (decimal)d.Closes[b] + Sum(b, 5, Range) / 25
                    && Body(b) > Body(a) - Sum(a, 5, Range) * 3 / 25
                    && Body(i) > Body(b) - Sum(b, 5, Range) * 3 / 25
                    && Body(i) > Sum(i, 10, Body) / 10;
            if (match)
                values[i] = white ? 100 : -100;
        }
        return new(First(name, d.Count), values);
    }

    internal sealed record Golden(string Name, Bar[] Bars, double Expected);

    private static (double O, double H, double L, double C) Black(
        double o,
        double c,
        double shadow = 0
    ) => (o, c - shadow + 10, c - shadow, c);

    private static (double O, double H, double L, double C) White(
        double o,
        double c,
        double shadow = 0
    ) => (o, c + shadow, c + shadow - 10, c);

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
        return new(name, bars.ToArray(), expected);
    }

    internal static readonly Golden[] Cases =
    [
        Case("ThreeBlackCrows", -100, (6, 10, 0, 8), Black(9, 5), Black(8, 4), Black(7, 3)),
        Case("ThreeBlackCrows", 0, (8, 10, 0, 6), Black(9, 5), Black(8, 4), Black(7, 3)),
        Case("ThreeBlackCrows", 0, (1, 5, -5, 4), Black(9, 5), Black(8, 4), Black(7, 3)),
        Case("ThreeBlackCrows", -100, (5, 10, 0, 5), Black(9, 5), Black(8, 4), Black(7, 3)),
        Case("ThreeBlackCrows", 0, (6, 10, 0, 8), Black(9, 5), Black(9, 4), Black(7, 3)),
        Case("ThreeBlackCrows", 0, (6, 10, 0, 8), Black(9, 5), Black(5, 4), Black(4.5, 3)),
        Case("ThreeBlackCrows", 0, (6, 10, 0, 8), Black(9, 5), Black(8, 4), Black(8, 3)),
        Case("ThreeBlackCrows", 0, (6, 10, 0, 8), Black(9, 5), Black(8, 4), Black(4, 3)),
        Case("ThreeBlackCrows", 0, (6, 10, 0, 8), Black(9, 5), Black(8, 5), Black(7, 3)),
        Case("ThreeBlackCrows", 0, (6, 10, 0, 8), Black(9, 5), Black(8, 4), Black(7, 4)),
        Case(
            "ThreeBlackCrows",
            -100,
            (6, 10, 0, 8),
            Black(5.5, 5),
            Black(5.25, 4.75),
            Black(5, 4.5)
        ),
        Case("IdenticalThreeCrows", -100, Black(9, 5), Black(5, 1), Black(1, -3)),
        Case("IdenticalThreeCrows", -100, Black(9, 5), Black(5.5, 1), Black(1, -3)),
        Case("IdenticalThreeCrows", -100, Black(9, 5), Black(4.5, 1), Black(1, -3)),
        Case("IdenticalThreeCrows", 0, Black(9, 5), Black(5.625, 1), Black(1, -3)),
        Case("IdenticalThreeCrows", 0, Black(9, 5), Black(4.375, 1), Black(1, -3)),
        Case("IdenticalThreeCrows", -100, Black(9, 5), Black(5, 1), Black(1.5, -3)),
        Case("IdenticalThreeCrows", -100, Black(9, 5), Black(5, 1), Black(.5, -3)),
        Case("IdenticalThreeCrows", 0, Black(9, 5), Black(5, 1), Black(1.625, -3)),
        Case("IdenticalThreeCrows", 0, Black(9, 5), Black(5, 1), Black(.375, -3)),
        Case("ThreeWhiteSoldiers", 100, White(1, 7), White(5, 11), White(9, 15)),
        Case("ThreeWhiteSoldiers", 100, White(1, 7), White(9, 11), White(10, 16)),
        Case("ThreeWhiteSoldiers", 0, White(1, 7), White(9.125, 11), White(10, 16)),
        Case("ThreeWhiteSoldiers", 100, White(1, 7), White(5, 11), White(13, 19)),
        Case("ThreeWhiteSoldiers", 0, White(1, 7), White(5, 11), White(13.125, 19)),
        Case("ThreeWhiteSoldiers", 0, White(1, 7), White(1, 11), White(9, 15)),
        Case("ThreeWhiteSoldiers", 0, White(1, 7), White(5, 11), White(5, 15)),
        Case("ThreeWhiteSoldiers", 0, White(1, 7), White(5, 7), White(9, 15)),
        Case("ThreeWhiteSoldiers", 0, White(1, 7), White(5, 11), White(9, 11)),
        Case("ThreeWhiteSoldiers", 0, White(0, 10), White(12, 16), White(15, 21)),
        Case("ThreeWhiteSoldiers", 100, White(0, 10), White(12, 16.125), White(15, 21)),
        Case("ThreeWhiteSoldiers", 0, White(0, 8), White(7, 17), White(19, 23)),
        Case("ThreeWhiteSoldiers", 100, White(0, 8), White(7, 17), White(19, 23.125)),
        Case("ThreeWhiteSoldiers", 0, White(1, 3), White(2, 4), White(3, 5)),
        Case("ThreeWhiteSoldiers", 100, White(1, 3), White(2, 4), White(3, 5.125)),
        Case("ThreeWhiteSoldiers", 100, White(1, 1), White(2, 6), White(5, 9)),
    ];

    internal static CompetitorData Fixture() => FromBars(Cases.SelectMany(c => c.Bars).ToArray());

    internal static CompetitorData FromBars(Bar[] bars) =>
        CompetitorData.FromOhlc(
            bars.Select(b => b.Open).ToArray(),
            bars.Select(b => b.High).ToArray(),
            bars.Select(b => b.Low).ToArray(),
            bars.Select(b => b.Close).ToArray()
        );
}

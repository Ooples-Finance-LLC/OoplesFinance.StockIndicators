using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class FiveCandleContinuationComparison
{
    internal static readonly string[] Names = ["MatHold", "RisingFallingThreeMethods"];
    internal static readonly ComparisonPair[] Pairs = Names
        .Select(n => new ComparisonPair(
            "TaLib.Candles." + n,
            n + "Candle",
            (d, _) => Competitor(n, d),
            (d, _) => Ooples(n, d),
            (d, _) => Reference(n, d),
            CompetitorReference: n == "MatHold"
                ? (d, _) => Reference(n, d, competitorArithmetic: true)
                : null
        ))
        .ToArray();

    internal static IIndicator Create(
        string name,
        int longPeriod = 10,
        int shortPeriod = 10,
        double penetration = .5
    ) =>
        name == "MatHold"
            ? new MatHoldCandle(longPeriod, shortPeriod, penetration)
            : new RisingFallingThreeMethodsCandle(longPeriod, shortPeriod);

    internal static ComparisonSeries Ooples(string name, CompetitorData d, double penetration = .5)
    {
        return CandleComparisonExecution.Run(
            Create(name, penetration: penetration),
            d,
            Math.Min(14, d.Count)
        );
    }

    internal static ComparisonSeries Competitor(
        string name,
        CompetitorData d,
        double penetration = .5
    )
    {
        var packed = new int[d.Count];
        System.Range range;
        var code =
            name == "MatHold"
                ? Candles.MatHold<double>(
                    d.Opens,
                    d.Highs,
                    d.Lows,
                    d.Closes,
                    System.Range.All,
                    packed,
                    out range,
                    penetration
                )
                : Candles.RisingFallingThreeMethods<double>(
                    d.Opens,
                    d.Highs,
                    d.Lows,
                    d.Closes,
                    System.Range.All,
                    packed,
                    out range
                );
        return CandleComparisonExecution.Unpack(
            code,
            packed,
            range,
            d.Count,
            Math.Min(14, d.Count),
            name
        );
    }

    internal static ComparisonSeries Reference(
        string name,
        CompetitorData d,
        double penetration = .5,
        bool competitorArithmetic = false
    )
    {
        var values = new double[d.Count];
        decimal Body(int i) => Math.Abs((decimal)d.Closes[i] - (decimal)d.Opens[i]);
        decimal Mean(int end) => Enumerable.Range(end - 10, 10).Sum(Body) / 10;
        bool White(int i) => d.Closes[i] >= d.Opens[i];
        for (var i = 14; i < d.Count; i++)
        {
            var a = i - 4;
            var b = i - 3;
            var c = i - 2;
            var e = i - 1;
            if (
                Body(a) <= Mean(a)
                || Body(b) >= Mean(b)
                || Body(c) >= Mean(c)
                || Body(e) >= Mean(e)
            )
                continue;
            bool match;
            if (name == "MatHold")
            {
                bool Allowed(int j) =>
                    Math.Min(d.Opens[j], d.Closes[j]) < d.Closes[a]
                    && PenetrationReferenceArithmetic.Passes(
                        d.Opens[a],
                        d.Closes[a],
                        Math.Min(d.Opens[j], d.Closes[j]),
                        -penetration,
                        true,
                        competitorArithmetic
                    );
                match =
                    White(a)
                    && !White(b)
                    && White(i)
                    && d.Closes[b] > d.Closes[a]
                    && Allowed(c)
                    && Allowed(e)
                    && Math.Max(d.Opens[c], d.Closes[c]) < d.Opens[b]
                    && Math.Max(d.Opens[e], d.Closes[e]) < Math.Max(d.Opens[c], d.Closes[c])
                    && d.Opens[i] > d.Closes[e]
                    && d.Closes[i] > Math.Max(Math.Max(d.Highs[b], d.Highs[c]), d.Highs[e]);
                if (match)
                    values[i] = 100;
            }
            else
            {
                var white = White(a);
                bool Overlaps(int j) =>
                    Math.Min(d.Opens[j], d.Closes[j]) < d.Highs[a]
                    && Math.Max(d.Opens[j], d.Closes[j]) > d.Lows[a];
                match =
                    Body(i) > Mean(i)
                    && white != White(b)
                    && white != White(c)
                    && white != White(e)
                    && white == White(i)
                    && Overlaps(b)
                    && Overlaps(c)
                    && Overlaps(e)
                    && (
                        white
                            ? d.Closes[c] < d.Closes[b]
                                && d.Closes[e] < d.Closes[c]
                                && d.Opens[i] > d.Closes[e]
                                && d.Closes[i] > d.Closes[a]
                            : d.Closes[c] > d.Closes[b]
                                && d.Closes[e] > d.Closes[c]
                                && d.Opens[i] < d.Closes[e]
                                && d.Closes[i] < d.Closes[a]
                    );
                if (match)
                    values[i] = white ? 100 : -100;
            }
        }
        return new(Math.Min(14, d.Count), values);
    }

    internal sealed record Golden(
        string Name,
        Bar[] Bars,
        double Penetration,
        double Expected,
        double? RoundedExpected = null
    );

    private static Golden Case(
        string name,
        double expected,
        (double O, double H, double L, double C)[] pattern,
        double penetration = .5
    )
    {
        var bars = Enumerable
            .Range(0, 10)
            .Select(i => new Bar(DateTime.UnixEpoch.AddDays(i), 4, 10, 0, 6, 1))
            .ToList();
        foreach (var b in pattern)
            bars.Add(new(DateTime.UnixEpoch.AddDays(bars.Count), b.O, b.H, b.L, b.C, 1));
        return new(name, bars.ToArray(), penetration, expected);
    }

    internal static readonly Golden[] Cases = BuildCases();

    private static Golden[] BuildCases()
    {
        var cases = new List<Golden>();
        (double O, double H, double L, double C)[] mat =
        [
            (2, 10, 0, 8),
            (11, 12, 2, 10),
            (9, 11, 1, 7),
            (8, 10, 0, 6),
            (7, 14, 4, 13),
        ];
        (double O, double H, double L, double C)[] rising =
        [
            (2, 10, 0, 8),
            (7, 10, 0, 6),
            (6, 10, 0, 5),
            (5, 10, 0, 4),
            (5, 11, 1, 10),
        ];
        cases.Add(Case("MatHold", 100, mat));
        cases.Add(Case("RisingFallingThreeMethods", 100, rising));
        void Change(
            string name,
            double expected,
            (double O, double H, double L, double C)[] original,
            int index,
            (double O, double H, double L, double C) bar
        )
        {
            var pattern = original.ToArray();
            pattern[index] = bar;
            cases.Add(Case(name, expected, pattern));
        }
        Change("MatHold", 0, mat, 0, (6, 10, 0, 8));
        Change("MatHold", 0, mat, 1, (10, 12, 2, 11));
        Change("MatHold", 0, mat, 1, (9, 12, 2, 8));
        Change("MatHold", 0, mat, 2, (10, 11, 1, 8));
        Change("MatHold", 0, mat, 3, (8, 10, 0, 8));
        Change("MatHold", 0, mat, 2, (11, 12, 1, 10));
        Change("MatHold", 0, mat, 3, (9, 10, 0, 7));
        Change("MatHold", 0, mat, 4, (6, 14, 4, 13));
        Change("MatHold", 0, mat, 4, (7, 14, 4, 12));
        Change("MatHold", 100, mat, 4, (13, 14, 4, 13));
        Change("MatHold", 100, mat, 2, (7, 11, 1, 9));
        cases.Add(
            Case(
                "MatHold",
                100,
                [(2, 10, 0, 8), (11, 12, 2, 10), (9, 11, 1, 7), (6, 10, 0, 8), (9, 14, 4, 13)]
            )
        );
        cases.Add(
            Case(
                "MatHold",
                100,
                [(2, 10, 0, 8), (11, 12, 2, 10), (9, 11, 1, 7), (7, 10, 0, 7), (8, 14, 4, 13)]
            )
        );
        Change("MatHold", 0, mat, 3, (6, 10, 0, 5));
        Change("MatHold", 100, mat, 3, (6, 10, 0, 5.125));
        cases.Add(
            Case(
                "MatHold",
                0,
                [(2, 10, 0, 8), (11, 12, 2, 10), (6.5, 11, 1, 5), (6, 10, 0, 5.5), (7, 14, 4, 13)]
            )
        );
        cases.Add(
            Case(
                "MatHold",
                100,
                [
                    (2, 10, 0, 8),
                    (11, 12, 2, 10),
                    (6.5, 11, 1, 5.125),
                    (6, 10, 0, 5.5),
                    (7, 14, 4, 13),
                ]
            )
        );
        cases.Add(
            Case(
                "MatHold",
                0,
                [(1, 10, 0, 8), (11, 12, 2, 8.5), (9, 11, 1, 7), (8, 10, 0, 6), (7, 14, 4, 13)]
            )
        );
        cases.Add(
            Case(
                "MatHold",
                100,
                [(1, 10, 0, 8), (11, 12, 2, 8.625), (9, 11, 1, 7), (8, 10, 0, 6), (7, 14, 4, 13)]
            )
        );
        cases.Add(Case("MatHold", 0, mat, 0));
        cases.Add(Case("MatHold", 100, mat, 2));
        cases.Add(
            Case(
                "MatHold",
                100,
                [
                    (2, 12, 0, 7),
                    (10, 10, 0, 9),
                    (7, 9, 0, 6.5),
                    (6.875, 8, 0, 6.625),
                    (7, 12, 0, 11),
                ],
                .1
            ) with
            {
                RoundedExpected = 0,
            }
        );
        Change("RisingFallingThreeMethods", 0, rising, 0, (6, 10, 0, 8));
        Change("RisingFallingThreeMethods", 0, rising, 1, (6, 10, 0, 7));
        Change("RisingFallingThreeMethods", 0, rising, 2, (5, 10, 0, 6));
        Change("RisingFallingThreeMethods", 0, rising, 3, (4, 10, 0, 5));
        Change("RisingFallingThreeMethods", 0, rising, 2, (7, 10, 0, 6));
        Change("RisingFallingThreeMethods", 0, rising, 3, (6, 10, 0, 5));
        Change("RisingFallingThreeMethods", 0, rising, 4, (4, 11, 1, 10));
        Change("RisingFallingThreeMethods", 0, rising, 4, (5, 11, 1, 8));
        Change("RisingFallingThreeMethods", 0, rising, 4, (15, 16, 6, 10));
        Change("RisingFallingThreeMethods", 100, rising, 1, (10.5, 11, 1, 9.5));
        Change("RisingFallingThreeMethods", 0, rising, 1, (11, 12, 2, 10));
        Change("RisingFallingThreeMethods", 100, rising, 1, (10.875, 12, 2, 9.875));
        Change("RisingFallingThreeMethods", 0, rising, 3, (0, 10, -10, -1));
        Change("RisingFallingThreeMethods", 100, rising, 3, (.125, 10, -10, -.875));
        // Integral scaled endpoints distinguish strict short/long body means exactly.
        void Scaled(
            string name,
            (double O, double H, double L, double C)[] original,
            int index,
            double open,
            double close,
            double expected
        )
        {
            var item = Case(name, expected, original);
            var bars = item
                .Bars.Select(b => new Bar(
                    b.Time,
                    b.Open * 10,
                    b.High * 10,
                    b.Low * 10,
                    b.Close * 10,
                    1
                ))
                .ToArray();
            var b = bars[10 + index];
            bars[10 + index] = new(b.Time, open, b.High, b.Low, close, 1);
            cases.Add(item with { Bars = bars });
        }
        Scaled("MatHold", mat, 2, 90, 67, 0);
        Scaled("MatHold", mat, 2, 90, 67.125, 100);
        Scaled("MatHold", mat, 3, 80, 57, 0);
        Scaled("MatHold", mat, 3, 80, 57.125, 100);
        Scaled("RisingFallingThreeMethods", rising, 1, 80, 56, 0);
        Scaled("RisingFallingThreeMethods", rising, 1, 80, 56.125, 100);
        Scaled("RisingFallingThreeMethods", rising, 2, 70, 47, 0);
        Scaled("RisingFallingThreeMethods", rising, 2, 70, 47.125, 100);
        Scaled("RisingFallingThreeMethods", rising, 3, 50, 28, 0);
        Scaled("RisingFallingThreeMethods", rising, 3, 50, 28.125, 100);
        Scaled("RisingFallingThreeMethods", rising, 4, 80, 101, 0);
        Scaled("RisingFallingThreeMethods", rising, 4, 80, 101.125, 100);
        var mirrors = cases.Select(c => new Golden(
            c.Name,
            c.Bars.Select(b => new Bar(b.Time, -b.Open, -b.Low, -b.High, -b.Close, 1)).ToArray(),
            c.Penetration,
            c.Name == "RisingFallingThreeMethods" ? -c.Expected : 0
        ));
        return cases.Concat(mirrors).ToArray();
    }

    internal static CompetitorData Fixture() =>
        CrowSoldierComparison.FromBars(Cases.SelectMany(c => c.Bars).ToArray());
}

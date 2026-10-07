using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class ExtendedReversalComparison
{
    internal static readonly string[] Names =
    [
        "Breakaway",
        "ConcealingBabySwallow",
        "LadderBottom",
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

    internal static IIndicator Create(string name, int period = 10) =>
        name switch
        {
            "Breakaway" => new BreakawayCandle(period),
            "ConcealingBabySwallow" => new ConcealingBabySwallowCandle(period),
            _ => new LadderBottomCandle(period),
        };

    private static int First(string name, int count) =>
        Math.Min(name == "ConcealingBabySwallow" ? 13 : 14, count);

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
            "Breakaway" => Candles.Breakaway<double>(
                d.Opens,
                d.Highs,
                d.Lows,
                d.Closes,
                System.Range.All,
                packed,
                out range
            ),
            "ConcealingBabySwallow" => Candles.ConcealingBabySwallow<double>(
                d.Opens,
                d.Highs,
                d.Lows,
                d.Closes,
                System.Range.All,
                packed,
                out range
            ),
            _ => Candles.LadderBottom<double>(
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
        decimal Body(int i) => Math.Abs((decimal)d.Closes[i] - (decimal)d.Opens[i]);
        decimal Threshold(int end, bool body) =>
            Enumerable
                .Range(end - 10, 10)
                .Sum(j => body ? Body(j) : (decimal)d.Highs[j] - (decimal)d.Lows[j])
            / (body ? 10 : 100);
        decimal Upper(int i) => (decimal)d.Highs[i] - (decimal)d.Opens[i];
        decimal Lower(int i) => (decimal)d.Closes[i] - (decimal)d.Lows[i];
        bool White(int i) => d.Closes[i] >= d.Opens[i];
        for (var i = First(name, d.Count); i < d.Count; i++)
        {
            var a = i - 4;
            var b = i - 3;
            var c = i - 2;
            var e = i - 1;
            bool match;
            if (name == "ConcealingBabySwallow")
                match =
                    !White(b)
                    && !White(c)
                    && !White(e)
                    && !White(i)
                    && Lower(b) < Threshold(b, false)
                    && Upper(b) < Threshold(b, false)
                    && Lower(c) < Threshold(c, false)
                    && Upper(c) < Threshold(c, false)
                    && d.Opens[e] < d.Closes[c]
                    && Upper(e) > Threshold(e, false)
                    && d.Highs[e] > d.Closes[c]
                    && d.Highs[i] > d.Highs[e]
                    && d.Lows[i] < d.Lows[e];
            else if (name == "LadderBottom")
                match =
                    !White(a)
                    && !White(b)
                    && !White(c)
                    && !White(e)
                    && d.Opens[a] > d.Opens[b]
                    && d.Opens[b] > d.Opens[c]
                    && d.Closes[a] > d.Closes[b]
                    && d.Closes[b] > d.Closes[c]
                    && Upper(e) > Threshold(e, false)
                    && White(i)
                    && d.Opens[i] > d.Opens[e]
                    && d.Closes[i] > d.Highs[e];
            else
            {
                var white = White(a);
                match =
                    Body(a) > Threshold(a, true)
                    && white == White(b)
                    && white == White(e)
                    && white != White(i)
                    && (
                        white
                            ? d.Opens[b] > d.Closes[a]
                                && d.Highs[c] > d.Highs[b]
                                && d.Lows[c] > d.Lows[b]
                                && d.Highs[e] > d.Highs[c]
                                && d.Lows[e] > d.Lows[c]
                                && d.Closes[i] < d.Opens[b]
                                && d.Closes[i] > d.Closes[a]
                            : d.Opens[b] < d.Closes[a]
                                && d.Highs[c] < d.Highs[b]
                                && d.Lows[c] < d.Lows[b]
                                && d.Highs[e] < d.Highs[c]
                                && d.Lows[e] < d.Lows[c]
                                && d.Closes[i] > d.Opens[b]
                                && d.Closes[i] < d.Closes[a]
                    );
                if (match)
                    values[i] = white ? -100 : 100;
                continue;
            }
            if (match)
                values[i] = 100;
        }
        return new(First(name, d.Count), values);
    }

    internal sealed record Golden(string Name, Bar[] Bars, double Expected);

    private static Golden Case(
        string name,
        double expected,
        (double O, double H, double L, double C)[] pattern
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

    internal static readonly Golden[] Cases = BuildCases();

    private static Golden[] BuildCases()
    {
        var cases = new List<Golden>();
        (double O, double H, double L, double C)[] breakaway =
        [
            (10, 12, 2, 4),
            (2, 3, -1, 0),
            (0, 2, -2, 1),
            (0, 1, -3, -2),
            (-1, 5, -2, 3),
        ];
        (double O, double H, double L, double C)[] swallow =
        [
            (10, 10, 0, 0),
            (8, 8, -2, -2),
            (-3, 0, -10, -5),
            (1, 1, -11, -11),
        ];
        (double O, double H, double L, double C)[] ladder =
        [
            (9, 10, 0, 5),
            (8, 10, 0, 4),
            (7, 10, 0, 3),
            (6, 8, -2, 2),
            (7, 10, 0, 9),
        ];
        cases.Add(Case("Breakaway", 100, breakaway));
        cases.Add(Case("ConcealingBabySwallow", 100, swallow));
        cases.Add(Case("LadderBottom", 100, ladder));
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
        Change("Breakaway", 0, breakaway, 0, (6, 12, 2, 4));
        Change("Breakaway", 0, breakaway, 1, (0, 3, -1, 2));
        Change("Breakaway", 0, breakaway, 1, (4, 5, -1, 0));
        Change("Breakaway", 0, breakaway, 2, (0, 3, -2, 1));
        Change("Breakaway", 0, breakaway, 2, (0, 2, -1, 1));
        Change("Breakaway", 0, breakaway, 3, (0, 2, -3, -2));
        Change("Breakaway", 0, breakaway, 3, (0, 1, -2, -2));
        Change("Breakaway", 0, breakaway, 3, (-2, 1, -3, 0));
        Change("Breakaway", 0, breakaway, 4, (-1, 5, -2, 2));
        Change("Breakaway", 0, breakaway, 4, (-1, 5, -2, 4));
        Change("Breakaway", 0, breakaway, 4, (4, 5, -2, 3));
        Change("Breakaway", 100, breakaway, 2, (1, 2, -2, 0));
        Change("Breakaway", 100, breakaway, 2, (0, 2, -2, 0));
        Change("Breakaway", 100, breakaway, 4, (3, 5, -2, 3));
        Change("ConcealingBabySwallow", 0, swallow, 0, (10, 11, 0, 0));
        Change("ConcealingBabySwallow", 0, swallow, 0, (10, 10, -1, 0));
        Change("ConcealingBabySwallow", 0, swallow, 1, (8, 9, -2, -2));
        Change("ConcealingBabySwallow", 0, swallow, 1, (8, 8, -3, -2));
        Change("ConcealingBabySwallow", 0, swallow, 2, (-2.5, -1.5, -10, -5));
        Change("ConcealingBabySwallow", 100, swallow, 2, (-2.5, -1.375, -10, -5));
        Change("ConcealingBabySwallow", 0, swallow, 2, (-2, 0, -10, -5));
        Change("ConcealingBabySwallow", 0, swallow, 2, (-4, -2, -10, -5));
        Change("ConcealingBabySwallow", 0, swallow, 3, (0, 0, -11, -11));
        Change("ConcealingBabySwallow", 0, swallow, 3, (1, 1, -10, -10));
        Change("ConcealingBabySwallow", 0, swallow, 3, (-11, 1, -11, 1));
        Change("ConcealingBabySwallow", 0, swallow, 0, (0, 10, 0, 10));
        Change("LadderBottom", 0, ladder, 1, (9, 10, 0, 4));
        Change("LadderBottom", 0, ladder, 2, (8, 10, 0, 3));
        Change("LadderBottom", 0, ladder, 1, (8, 10, 0, 5));
        Change("LadderBottom", 0, ladder, 2, (7, 10, 0, 4));
        Change("LadderBottom", 0, ladder, 3, (6, 7, -3, 2));
        Change("LadderBottom", 100, ladder, 3, (6, 7.125, -2.875, 2));
        Change("LadderBottom", 0, ladder, 4, (6, 10, 0, 9));
        Change("LadderBottom", 0, ladder, 4, (7, 10, 0, 8));
        Change("LadderBottom", 100, ladder, 4, (9, 10, 0, 9));
        Change("LadderBottom", 0, ladder, 3, (2, 8, -2, 6));
        cases.Add(
            Case(
                "LadderBottom",
                100,
                [(9, 10, 0, 5), (8, 10, 0, 4), (7, 10, 0, 3), (10, 12, 2, 9), (11, 15, 5, 13)]
            )
        );
        var mirrors = cases.Select(c => new Golden(
            c.Name,
            c.Bars.Select(b => new Bar(b.Time, -b.Open, -b.Low, -b.High, -b.Close, 1)).ToArray(),
            c.Name == "Breakaway" && c.Bars[^1].Open.CompareTo(c.Bars[^1].Close) != 0
                ? -c.Expected
                : 0
        ));
        return cases.Concat(mirrors).ToArray();
    }

    internal static CompetitorData Fixture() =>
        CrowSoldierComparison.FromBars(Cases.SelectMany(c => c.Bars).ToArray());
}

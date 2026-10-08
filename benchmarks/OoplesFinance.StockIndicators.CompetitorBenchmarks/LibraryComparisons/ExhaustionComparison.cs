using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class ExhaustionComparison
{
    internal static readonly string[] Names = ["AdvanceBlock", "Stalled", "ThreeStarsInSouth"];
    internal static readonly ComparisonPair[] Pairs = Names
        .Select(n => new ComparisonPair(
            "TaLib.Candles." + n,
            n + "Candle",
            (d, _) => Competitor(n, d),
            (d, _) => CandleComparisonExecution.Run(Create(n), d, Math.Min(12, d.Count)),
            (d, _) => Reference(n, d)
        ))
        .ToArray();

    internal static IIndicator Create(
        string name,
        int longPeriod = 10,
        int shortPeriod = 10,
        int shadowPeriod = 10,
        int nearPeriod = 5,
        int farPeriod = 5
    ) =>
        name switch
        {
            "AdvanceBlock" => new AdvanceBlockCandle(
                longPeriod,
                shadowPeriod,
                nearPeriod,
                farPeriod
            ),
            "Stalled" => new StalledCandle(longPeriod, shortPeriod, shadowPeriod, nearPeriod),
            _ => new ThreeStarsInSouthCandle(longPeriod, shortPeriod, shadowPeriod),
        };

    private static ComparisonSeries Competitor(string name, CompetitorData d)
    {
        var packed = new int[d.Count];
        System.Range range;
        var code = name switch
        {
            "AdvanceBlock" => Candles.AdvanceBlock<double>(
                d.Opens,
                d.Highs,
                d.Lows,
                d.Closes,
                System.Range.All,
                packed,
                out range
            ),
            "Stalled" => Candles.Stalled<double>(
                d.Opens,
                d.Highs,
                d.Lows,
                d.Closes,
                System.Range.All,
                packed,
                out range
            ),
            _ => Candles.ThreeStarsInSouth<double>(
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
        decimal Body(int i) => Math.Abs((decimal)d.Closes[i] - (decimal)d.Opens[i]);
        decimal Range(int i) => (decimal)d.Highs[i] - (decimal)d.Lows[i];
        decimal Bodies(int end) => Enumerable.Range(end - 10, 10).Sum(Body) / 10;
        decimal VeryShort(int end) => Enumerable.Range(end - 10, 10).Sum(Range) / 100;
        decimal Short(int end) => Enumerable.Range(end - 10, 10).Sum(j => Range(j) - Body(j)) / 20;
        decimal Near(int end) => Enumerable.Range(end - 5, 5).Sum(Range) / 25;
        decimal Far(int end) => Enumerable.Range(end - 5, 5).Sum(Range) * 3 / 25;
        decimal Upper(int i) => (decimal)d.Highs[i] - (decimal)d.Closes[i];
        for (var i = 12; i < d.Count; i++)
        {
            var a = i - 2;
            var b = i - 1;
            var ba = Body(a);
            var bb = Body(b);
            var bc = Body(i);
            bool match;
            if (name == "ThreeStarsInSouth")
                match =
                    d.Closes[a] < d.Opens[a]
                    && d.Closes[b] < d.Opens[b]
                    && d.Closes[i] < d.Opens[i]
                    && ba > Bodies(a)
                    && (decimal)d.Closes[a] - (decimal)d.Lows[a] > ba
                    && bb < ba
                    && d.Opens[b] > d.Closes[a]
                    && d.Opens[b] <= d.Highs[a]
                    && d.Lows[b] < d.Closes[a]
                    && d.Lows[b] >= d.Lows[a]
                    && (decimal)d.Closes[b] - (decimal)d.Lows[b] > VeryShort(b)
                    && bc < Bodies(i)
                    && (decimal)d.Closes[i] - (decimal)d.Lows[i] < VeryShort(i)
                    && (decimal)d.Highs[i] - (decimal)d.Opens[i] < VeryShort(i)
                    && d.Lows[i] > d.Lows[b]
                    && d.Highs[i] < d.Highs[b];
            else
            {
                if (
                    d.Closes[a] < d.Opens[a]
                    || d.Closes[b] < d.Opens[b]
                    || d.Closes[i] < d.Opens[i]
                    || d.Closes[i] <= d.Closes[b]
                    || d.Closes[b] <= d.Closes[a]
                    || ba <= Bodies(a)
                    || d.Opens[b] <= d.Opens[a]
                    || (decimal)d.Opens[b] > (decimal)d.Closes[a] + Near(a)
                )
                    continue;
                if (name == "Stalled")
                    match =
                        bb > Bodies(b)
                        && Upper(b) < VeryShort(b)
                        && bc < Bodies(i)
                        && (decimal)d.Opens[i] >= (decimal)d.Closes[b] - bc - Near(b);
                else
                    match =
                        d.Opens[i] > d.Opens[b]
                        && (decimal)d.Opens[i] <= (decimal)d.Closes[b] + Near(b)
                        && Upper(a) < Short(a)
                        && (
                            bb < ba - Far(a) && bc < bb + Near(b)
                            || bc < bb - Far(b)
                            || bc < bb && bb < ba && (Upper(i) > Short(i) || Upper(b) > Short(b))
                            || bc < bb && Upper(i) > bc
                        );
            }
            if (match)
                values[i] = name == "ThreeStarsInSouth" ? 100 : -100;
        }
        return new(Math.Min(12, d.Count), values);
    }

    internal sealed record Golden(string Name, Bar[] Bars, double Expected);

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

    internal static readonly Golden[] Cases = BuildCases();

    private static Golden[] BuildCases()
    {
        var cases = new List<Golden>();
        (double O, double H, double L, double C)[] first =
        [
            (0, 10, 0, 10),
            (11, 14, 4, 14),
            (14, 18, 8, 18),
        ];
        (double O, double H, double L, double C)[] second =
        [
            (0, 6, -4, 6),
            (5, 15, 5, 15),
            (14, 17, 7, 17),
        ];
        (double O, double H, double L, double C)[] third =
        [
            (0, 10, 0, 10),
            (8, 16, 6, 14),
            (13, 21, 11, 17),
        ];
        (double O, double H, double L, double C)[] fourth =
        [
            (0, 6, -4, 6),
            (5, 11, 1, 11),
            (10, 19, 9, 14),
        ];
        (double O, double H, double L, double C)[] stalled =
        [
            (0, 10, 0, 6),
            (5, 11, 1, 11),
            (11, 15, 5, 12),
        ];
        (double O, double H, double L, double C)[] south =
        [
            (10, 11, 1, 6),
            (9, 10, 4, 6),
            (8, 8, 7, 7),
        ];
        foreach (var pattern in new[] { first, second, third, fourth })
            cases.Add(Case("AdvanceBlock", -100, pattern));
        cases.Add(Case("Stalled", -100, stalled));
        cases.Add(Case("ThreeStarsInSouth", 100, south));
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
        // Each of the four Advance Block alternatives has independent strict endpoints.
        cases.Add(Case("AdvanceBlock", 0, (0, 10, 0, 10), (11, 15, 5, 15), (15, 20, 10, 20)));
        cases.Add(
            Case(
                "AdvanceBlock",
                -100,
                (0, 10, 0, 10),
                (11, 14.875, 4.875, 14.875),
                (15, 20, 10, 20)
            )
        );
        Change("AdvanceBlock", 0, first, 2, (14, 19, 9, 19));
        Change("AdvanceBlock", -100, first, 2, (14, 18.875, 8.875, 18.875));
        Change("AdvanceBlock", 0, second, 2, (14, 18, 8, 18));
        Change("AdvanceBlock", -100, second, 2, (14, 17.875, 7.875, 17.875));
        Change("AdvanceBlock", 0, fourth, 2, (10, 18, 8, 14));
        Change("AdvanceBlock", -100, fourth, 2, (10, 18.125, 8.125, 14));
        cases.Add(Case("AdvanceBlock", -100, (0, 10, 0, 10), (12, 15, 5, 15), (15, 19, 9, 19)));
        cases.Add(
            Case(
                "AdvanceBlock",
                0,
                (0, 10, 0, 10),
                (12.125, 15.125, 5.125, 15.125),
                (15, 19, 9, 19)
            )
        );
        Change("AdvanceBlock", 0, third, 2, (8, 18, 8, 17));
        Change("AdvanceBlock", 0, third, 2, (16.125, 21, 11, 17));
        Change("AdvanceBlock", 0, third, 2, (13, 21, 11, 14));
        Change("AdvanceBlock", 0, third, 0, (0, 14, 0, 10));
        Change("AdvanceBlock", -100, third, 0, (0, 13.875, 0, 10));
        Change("Stalled", -100, stalled, 2, (12, 15, 5, 12));
        Change("Stalled", 0, stalled, 0, (4, 10, 0, 6));
        Change("Stalled", 0, stalled, 1, (5, 12, 2, 11));
        Change("Stalled", -100, stalled, 1, (5, 11.875, 1.875, 11));
        Change("Stalled", 0, stalled, 2, (11, 15, 5, 11));
        cases.Add(Case("Stalled", -100, (0, 10, 0, 6), (8, 14, 4, 14), (14, 18, 8, 15)));
        cases.Add(
            Case(
                "Stalled",
                0,
                (0, 10, 0, 6),
                (8.125, 14.125, 4.125, 14.125),
                (14.125, 18, 8, 15.125)
            )
        );
        Change("ThreeStarsInSouth", 0, south, 0, (10, 11, 2, 6));
        Change("ThreeStarsInSouth", 0, south, 1, (9, 10, 5, 6));
        Change("ThreeStarsInSouth", 100, south, 1, (9, 10, 1, 6));
        Change("ThreeStarsInSouth", 0, south, 1, (9, 10, .875, 6));
        Change("ThreeStarsInSouth", 100, south, 1, (11, 12, 4, 8));
        Change("ThreeStarsInSouth", 0, south, 1, (11.125, 12, 4, 8.125));
        Change("ThreeStarsInSouth", 0, south, 1, (6, 10, 3, 5));
        Change("ThreeStarsInSouth", 100, south, 1, (6.125, 10, 3, 5));
        Change("ThreeStarsInSouth", 0, south, 2, (10, 10, 9, 9));
        Change("ThreeStarsInSouth", 100, south, 2, (9.875, 9.875, 8.875, 8.875));
        Change("ThreeStarsInSouth", 0, south, 2, (5, 5, 4, 4));
        Change("ThreeStarsInSouth", 100, south, 2, (5.125, 5.125, 4.125, 4.125));
        void Scaled(
            string name,
            double expected,
            (double O, double H, double L, double C)[] original,
            double scale,
            int index,
            (double O, double H, double L, double C) b
        )
        {
            var item = Case(name, expected, original);
            var bars = item
                .Bars.Select(v => new Bar(
                    v.Time,
                    v.Open * scale,
                    v.High * scale,
                    v.Low * scale,
                    v.Close * scale,
                    1
                ))
                .ToArray();
            bars[10 + index] = new(bars[10 + index].Time, b.O, b.H, b.L, b.C, 1);
            cases.Add(item with { Bars = bars });
        }
        Scaled("AdvanceBlock", 0, third, 10, 2, (130, 204, 104, 170));
        Scaled("AdvanceBlock", -100, third, 10, 2, (130, 204.125, 104.125, 170));
        Scaled("Stalled", 0, stalled, 10, 2, (110, 150, 50, 138));
        Scaled("Stalled", -100, stalled, 10, 2, (110, 150, 50, 137.875));
        Scaled("Stalled", 0, stalled, 10, 1, (50, 74, -26, 74));
        Scaled("Stalled", -100, stalled, 10, 1, (50, 74.125, -25.875, 74.125));
        Scaled("ThreeStarsInSouth", 0, south, 100, 2, (800, 896, 700, 700));
        Scaled("ThreeStarsInSouth", 100, south, 100, 2, (800, 895.875, 700, 700));
        Scaled("ThreeStarsInSouth", 0, south, 100, 2, (800, 800, 604, 700));
        Scaled("ThreeStarsInSouth", 100, south, 100, 2, (800, 800, 604.125, 700));
        Scaled("ThreeStarsInSouth", 0, south, 10, 2, (80, 80, 57, 57));
        Scaled("ThreeStarsInSouth", 100, south, 10, 2, (80, 80, 57.125, 57.125));
        // Each defining color condition is separate from the mirrored-direction non-pattern.
        foreach (
            var (name, pattern) in new[]
            {
                ("AdvanceBlock", third),
                ("Stalled", stalled),
                ("ThreeStarsInSouth", south),
            }
        )
            for (var i = 0; i < 3; i++)
            {
                var b = pattern[i];
                Change(name, 0, pattern, i, (b.C, b.H, b.L, b.O));
            }
        return cases
            .Concat(
                cases.Select(c => new Golden(
                    c.Name,
                    c.Bars.Select(b => new Bar(b.Time, -b.Open, -b.Low, -b.High, -b.Close, 1))
                        .ToArray(),
                    0
                ))
            )
            .ToArray();
    }

    internal static CompetitorData Fixture() =>
        CrowSoldierComparison.FromBars(Cases.SelectMany(c => c.Bars).ToArray());
}

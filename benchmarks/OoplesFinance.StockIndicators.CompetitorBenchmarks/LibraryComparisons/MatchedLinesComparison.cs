using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class MatchedLinesComparison
{
    internal static readonly string[] Names =
    [
        "Counterattack",
        "SeparatingLines",
        "GapSideBySideWhiteLines",
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
        int first = 10,
        int second = 5,
        int third = 10
    ) =>
        name switch
        {
            "Counterattack" => new CounterattackCandle(first, second),
            "SeparatingLines" => new SeparatingLinesCandle(first, third, second),
            _ => new GapSideBySideWhiteLinesCandle(first, second),
        };

    private static int First(string name, int count) =>
        Math.Min(name == "GapSideBySideWhiteLines" ? 7 : 11, count);

    private static ComparisonSeries Ooples(string name, CompetitorData d)
    {
        return CandleComparisonExecution.Run(
            Create(name, name == "GapSideBySideWhiteLines" ? 5 : 10),
            d,
            First(name, d.Count)
        );
    }

    private static ComparisonSeries Competitor(string name, CompetitorData d)
    {
        var packed = new int[d.Count];
        System.Range range;
        var code = name switch
        {
            "Counterattack" => Candles.Counterattack<double>(
                d.Opens,
                d.Highs,
                d.Lows,
                d.Closes,
                System.Range.All,
                packed,
                out range
            ),
            "SeparatingLines" => Candles.SeparatingLines<double>(
                d.Opens,
                d.Highs,
                d.Lows,
                d.Closes,
                System.Range.All,
                packed,
                out range
            ),
            _ => Candles.GapSideBySideWhiteLines<double>(
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
        decimal Range(int i) => (decimal)d.Highs[i] - (decimal)d.Lows[i];
        decimal Sum(int end, int count, Func<int, decimal> term) =>
            Enumerable.Range(end - count, count).Sum(term);
        for (var i = First(name, d.Count); i < d.Count; i++)
        {
            var p = i - 1;
            var equal = Sum(p, 5, Range) / 100;
            if (name == "GapSideBySideWhiteLines")
            {
                var a = i - 2;
                if (d.Closes[p] < d.Opens[p] || d.Closes[i] < d.Opens[i])
                    continue;
                var up =
                    Math.Min(d.Opens[p], d.Closes[p]) > Math.Max(d.Opens[a], d.Closes[a])
                    && Math.Min(d.Opens[i], d.Closes[i]) > Math.Max(d.Opens[a], d.Closes[a]);
                var down =
                    Math.Max(d.Opens[p], d.Closes[p]) < Math.Min(d.Opens[a], d.Closes[a])
                    && Math.Max(d.Opens[i], d.Closes[i]) < Math.Min(d.Opens[a], d.Closes[a]);
                if (
                    (up || down)
                    && Math.Abs((decimal)d.Opens[i] - (decimal)d.Opens[p]) <= equal
                    && Math.Abs(Body(i) - Body(p)) <= Sum(p, 5, Range) / 25
                )
                    values[i] = up ? 100 : -100;
            }
            else
            {
                if (
                    (d.Closes[p] < d.Opens[p]) == (d.Closes[i] < d.Opens[i])
                    || Body(i) <= Sum(i, 10, Body) / 10
                )
                    continue;
                if (name == "Counterattack")
                {
                    if (
                        Body(p) <= Sum(p, 10, Body) / 10
                        || Math.Abs((decimal)d.Closes[i] - (decimal)d.Closes[p]) > equal
                    )
                        continue;
                }
                else
                {
                    if (Math.Abs((decimal)d.Opens[i] - (decimal)d.Opens[p]) > equal)
                        continue;
                    var shadow =
                        d.Closes[i] >= d.Opens[i]
                            ? (decimal)d.Opens[i] - (decimal)d.Lows[i]
                            : (decimal)d.Highs[i] - (decimal)d.Opens[i];
                    if (shadow >= Sum(i, 10, Range) / 100)
                        continue;
                }
                values[i] = d.Closes[i] >= d.Opens[i] ? 100 : -100;
            }
        }
        return new(First(name, d.Count), values);
    }

    internal sealed record Golden(string Name, Bar[] Bars, double Expected);

    internal static readonly Golden[] Cases =
    [
        Case("Counterattack", 100, (8, 10, 0, 2), (-3, 10, -4, 2)),
        Case("Counterattack", 100, (8, 10, 0, 2), (-3, 10, -4, 1.5)),
        Case("Counterattack", 100, (8, 10, 0, 2), (-3, 10, -4, 2.5)),
        Case("Counterattack", 0, (8, 10, 0, 2), (-3, 10, -4, 1.375)),
        Case("Counterattack", 0, (8, 10, 0, 2), (-3, 10, -4, 2.625)),
        Case("Counterattack", 0, (9, 10, 0, 2), (0, 10, 0, 2.5)),
        Case("Counterattack", 100, (9, 10, 0, 2), (-.125, 10, -1, 2.5)),
        Case("Counterattack", 0, (4, 10, 0, 2), (-3, 10, -4, 2)),
        Case("Counterattack", 0, (8, 10, 0, 2), (8, 10, 0, 2)),
        Case("SeparatingLines", 100, (5, 10, 0, 4), (5, 10, 4.5, 9)),
        Case("SeparatingLines", 100, (5, 10, 0, 4), (4.5, 10, 4, 9)),
        Case("SeparatingLines", 100, (5, 10, 0, 4), (5.5, 10, 5, 9)),
        Case("SeparatingLines", 0, (5, 10, 0, 4), (4.375, 10, 4, 9)),
        Case("SeparatingLines", 0, (5, 10, 0, 4), (5.625, 10, 5, 9)),
        Case("SeparatingLines", 0, (5, 10, 0, 4), (5, 10, 4, 9)),
        Case("SeparatingLines", 100, (5, 10, 0, 4), (5, 10, 4.125, 9)),
        Case("SeparatingLines", 0, (5, 10, 0, 3), (5, 10, 4.5, 7)),
        Case("SeparatingLines", 100, (5, 10, 0, 3), (5, 10, 4.5, 7.125)),
        Case("SeparatingLines", 100, (5, 100, 0, 4), (5, 10, 4, 9)),
        Case("GapSideBySideWhiteLines", 100, (4, 10, 0, 6), (8, 20, 0, 10), (8, 20, 0, 12)),
        Case("GapSideBySideWhiteLines", 0, (4, 10, 0, 6), (8, 20, 0, 10), (8, 20, 0, 12.125)),
        Case("GapSideBySideWhiteLines", 100, (4, 10, 0, 6), (8, 20, 0, 10), (7.5, 20, 0, 9.5)),
        Case("GapSideBySideWhiteLines", 100, (4, 10, 0, 6), (8, 20, 0, 10), (8.5, 20, 0, 10.5)),
        Case("GapSideBySideWhiteLines", 0, (4, 10, 0, 6), (8, 20, 0, 10), (7.375, 20, 0, 9.375)),
        Case("GapSideBySideWhiteLines", 100, (4, 10, 0, 6), (8, 20, 0, 10), (8, 20, 0, 8)),
        Case("GapSideBySideWhiteLines", 0, (4, 10, 0, 6), (6.5, 20, 0, 8.5), (6, 20, 0, 8)),
        Case("GapSideBySideWhiteLines", -100, (14, 20, 10, 16), (8, 20, 0, 10), (8, 20, 0, 12)),
        Case("GapSideBySideWhiteLines", 0, (0, 10, 0, 2), (8, 20, 0, 6), (8, 20, 0, 10)),
        Case("GapSideBySideWhiteLines", 0, (0, 10, 0, 2), (8, 20, 0, 10), (8, 20, 0, 6)),
    ];

    private static Golden Case(
        string name,
        double expected,
        params (double O, double H, double L, double C)[] candles
    )
    {
        var warm = name == "GapSideBySideWhiteLines" ? 5 : 10;
        var bars = Enumerable
            .Range(0, warm)
            .Select(i => new Bar(DateTime.UnixEpoch.AddDays(i), 4, 10, 0, 6, 1))
            .ToList();
        foreach (var c in candles)
            bars.Add(new(DateTime.UnixEpoch.AddDays(bars.Count), c.O, c.H, c.L, c.C, 1));
        return new(name, bars.ToArray(), expected);
    }

    internal static CompetitorData Fixture() => FromBars(Cases.SelectMany(c => c.Bars).ToArray());

    internal static CompetitorData FromBars(Bar[] bars) =>
        CompetitorData.FromOhlc(
            bars.Select(b => b.Open).ToArray(),
            bars.Select(b => b.High).ToArray(),
            bars.Select(b => b.Low).ToArray(),
            bars.Select(b => b.Close).ToArray()
        );
}

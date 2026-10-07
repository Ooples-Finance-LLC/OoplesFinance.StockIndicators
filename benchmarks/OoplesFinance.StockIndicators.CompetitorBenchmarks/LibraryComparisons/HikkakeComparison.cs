using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class HikkakeComparison
{
    internal static readonly string[] Names = ["Hikkake", "HikkakeModified"];
    internal static readonly ComparisonPair[] Pairs = Names
        .Select(n => new ComparisonPair(
            "TaLib.Candles." + n,
            n == "Hikkake" ? "HikkakeCandle" : "ModifiedHikkakeCandle",
            (d, _) => Competitor(n, d),
            (d, _) => Ooples(n, d),
            (d, _) => Reference(n, d)
        ))
        .ToArray();

    internal static IIndicator Create(string name, int period = 5) =>
        name == "Hikkake" ? new HikkakeCandle() : new ModifiedHikkakeCandle(period);

    private static int First(string name, int count) => Math.Min(name == "Hikkake" ? 5 : 10, count);

    private static ComparisonSeries Ooples(string name, CompetitorData d)
    {
        return CandleComparisonExecution.Run(Create(name), d, First(name, d.Count));
    }

    private static ComparisonSeries Competitor(string name, CompetitorData d)
    {
        var packed = new int[d.Count];
        System.Range range;
        var code =
            name == "Hikkake"
                ? Candles.Hikkake<double>(
                    d.Opens,
                    d.Highs,
                    d.Lows,
                    d.Closes,
                    System.Range.All,
                    packed,
                    out range
                )
                : Candles.HikkakeModified<double>(
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
            First(name, d.Count),
            name
        );
    }

    private static ComparisonSeries Reference(string name, CompetitorData d)
    {
        var values = new double[d.Count];
        var modified = name != "Hikkake";
        var lastFormation = -4;
        var direction = 0;
        for (var i = modified ? 7 : 2; i < d.Count; i++)
        {
            var a = i - 2;
            var b = i - 1;
            var formed = 0;
            if (d.Highs[b] < d.Highs[a] && d.Lows[b] > d.Lows[a])
                formed =
                    d.Highs[i] < d.Highs[b] && d.Lows[i] < d.Lows[b] ? 1
                    : d.Highs[i] > d.Highs[b] && d.Lows[i] > d.Lows[b] ? -1
                    : 0;
            if (formed != 0 && modified)
            {
                var tolerance =
                    Enumerable.Range(a - 5, 5).Sum(j => (decimal)d.Highs[j] - (decimal)d.Lows[j])
                    / 25;
                var distance =
                    formed > 0
                        ? (decimal)d.Closes[a] - (decimal)d.Lows[a]
                        : (decimal)d.Highs[a] - (decimal)d.Closes[a];
                if (
                    d.Highs[a] >= d.Highs[a - 1]
                    || d.Lows[a] <= d.Lows[a - 1]
                    || distance > tolerance
                )
                    formed = 0;
            }
            var signal = 0;
            if (formed != 0)
            {
                direction = formed;
                lastFormation = i;
                signal = formed * 100;
            }
            else if (
                direction != 0
                && i - lastFormation <= 3
                && (
                    direction > 0
                        ? d.Closes[i] > d.Highs[lastFormation - 1]
                        : d.Closes[i] < d.Lows[lastFormation - 1]
                )
            )
            {
                signal = direction * 200;
                direction = 0;
            }
            if (i >= (modified ? 10 : 5))
                values[i] = signal;
        }
        return new(First(name, d.Count), values);
    }

    internal sealed record Golden(string Name, Bar[] Bars, double[] Expected);

    private static (double H, double L, double C) B(double h, double l, double c) => (h, l, c);

    private static Golden Case(
        string name,
        (double H, double L, double C)[] candles,
        params (int Index, double Value)[] signals
    )
    {
        var bars = candles
            .Select((b, i) => new Bar(DateTime.UnixEpoch.AddDays(i), b.C, b.H, b.L, b.C, 1))
            .ToArray();
        var expected = new double[bars.Length];
        foreach (var (i, value) in signals)
            expected[i] = value;
        return new(name, bars, expected);
    }

    internal static readonly Golden[] Cases = BuildCases();

    private static Golden[] BuildCases()
    {
        var cases = new List<Golden>();
        foreach (var name in Names)
        {
            var modified = name == "HikkakeModified";
            var warm = modified ? 10 : 5;
            var prefix = Enumerable.Repeat(B(10, 0, 5), modified ? 8 : 4).ToList();
            if (modified)
                prefix.Add(B(8, 2, 4));
            prefix.Add(B(modified ? 7 : 8, modified ? 3 : 2, 5));
            prefix.Add(B(modified ? 6 : 7, modified ? 2 : 1, 4));
            var insideHigh = modified ? 7 : 8;
            var low = modified ? 2 : 1;
            for (var delay = 1; delay <= 4; delay++)
            {
                var candles = prefix
                    .Concat(Enumerable.Repeat(B(insideHigh, low, insideHigh), delay - 1))
                    .Append(B(10, low, 9))
                    .Append(B(10, low, 9))
                    .ToArray();
                cases.Add(
                    delay <= 3
                        ? Case(name, candles, (warm, 100), (warm + delay, 200))
                        : Case(name, candles, (warm, 100))
                );
            }
            // The same formation starts three bars before visible output and confirms on its first bar.
            var early = prefix
                .Skip(3)
                .Concat(
                    new[]
                    {
                        B(insideHigh, low, insideHigh),
                        B(insideHigh, low, insideHigh - 1),
                        B(10, low, 9),
                    }
                )
                .ToArray();
            cases.Add(Case(name, early, (warm, 200)));
            var consumed = prefix
                .Skip(3)
                .Concat(new[] { B(10, low, 9), B(10, low, 9), B(10, low, 9) })
                .ToArray();
            cases.Add(Case(name, consumed));
            // Strict inside and breakout range endpoints.
            var equalInside = prefix.ToArray();
            equalInside[^2] = B(modified ? 8 : 10, modified ? 3 : 2, 5);
            cases.Add(Case(name, equalInside));
            var equalLow = prefix.ToArray();
            equalLow[^2] = B(insideHigh, modified ? 2 : 0, 5);
            cases.Add(Case(name, equalLow));
            var equalBreakHigh = prefix.ToArray();
            equalBreakHigh[^1] = B(insideHigh, low, 4);
            cases.Add(Case(name, equalBreakHigh));
            var equalBreakLow = prefix.ToArray();
            equalBreakLow[^1] = B(modified ? 6 : 7, modified ? 3 : 2, 4);
            cases.Add(Case(name, equalBreakLow));
            if (modified)
            {
                var outsideNear = prefix.ToArray();
                outsideNear[^3] = B(8, 2, 4.125);
                cases.Add(Case(name, outsideNear));
                var outerEquality = prefix.ToArray();
                outerEquality[^3] = B(10, 2, 4);
                cases.Add(Case(name, outerEquality));
                var priority = prefix.ToArray();
                priority[^2] = B(7, 3, 6);
                cases.Add(
                    Case(
                        name,
                        priority
                            .Concat(new[] { B(5, 3, 4), B(4.5, 3.5, 4), B(9, 4, 8), B(9, 0, 2) })
                            .ToArray(),
                        (10, 100),
                        (13, -100),
                        (14, -200)
                    )
                );
            }
            else
                cases.Add(
                    Case(
                        name,
                        prefix.Concat(new[] { B(6, 2, 5), B(9, 3, 8.5), B(9, 0, 1) }).ToArray(),
                        (5, 100),
                        (7, -100),
                        (8, -200)
                    )
                );
        }
        return cases
            .Concat(
                cases.Select(c => new Golden(
                    c.Name,
                    c.Bars.Select(b => new Bar(b.Time, -b.Open, -b.Low, -b.High, -b.Close, 1))
                        .ToArray(),
                    c.Expected.Select(v => -v).ToArray()
                ))
            )
            .ToArray();
    }

    internal static CompetitorData Fixture() =>
        CrowSoldierComparison.FromBars(Cases.SelectMany(c => c.Bars).ToArray());
}

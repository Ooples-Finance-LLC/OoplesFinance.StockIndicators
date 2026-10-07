using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class PenetrationCandleComparison
{
    internal static readonly string[] Names = ["PiercingLine", "DarkCloudCover"];
    internal static readonly ComparisonPair[] Pairs = Names.Select(name => Pair(name)).ToArray();

    internal static IIndicator Create(string name, int period = 10, double penetration = .5) =>
        name == "PiercingLine"
            ? new PiercingLineCandle(period)
            : new DarkCloudCoverCandle(period, penetration);

    internal static ComparisonPair Pair(string name, double penetration = .5) =>
        new(
            "TaLib.Candles." + name,
            name + "Candle",
            (data, _) => Competitor(name, data, penetration),
            (data, _) => Ooples(name, data, penetration),
            (data, _) => Reference(name, data, penetration)
        );

    private static ComparisonSeries Ooples(string name, CompetitorData data, double penetration)
    {
        var indicator = Create(name, penetration: penetration);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return new(Math.Min(11, data.Count), run[indicator.Outputs[0]].ToArray());
    }

    private static ComparisonSeries Competitor(string name, CompetitorData data, double penetration)
    {
        var packed = new int[data.Count];
        System.Range range;
        var code =
            name == "PiercingLine"
                ? Candles.PiercingLine<double>(
                    data.Opens,
                    data.Highs,
                    data.Lows,
                    data.Closes,
                    System.Range.All,
                    packed,
                    out range
                )
                : Candles.DarkCloudCover<double>(
                    data.Opens,
                    data.Highs,
                    data.Lows,
                    data.Closes,
                    System.Range.All,
                    packed,
                    out range,
                    penetration
                );
        if (data.Count == 1 && code == TALib.Core.RetCode.OutOfRangeParam)
            return new(1, [double.NaN]);
        if (code != TALib.Core.RetCode.Success)
            throw new InvalidOperationException("TA-Lib returned " + code);
        var first = Math.Min(11, data.Count);
        var (start, count) = range.GetOffsetAndLength(data.Count);
        if (count != data.Count - first || count > 0 && start != first)
            throw new InvalidOperationException("Unexpected penetration alignment.");
        var values = new double[data.Count];
        for (var i = 0; i < count; i++)
            values[start + i] = packed[i];
        return new(first, values);
    }

    private static ComparisonSeries Reference(string name, CompetitorData data, double penetration)
    {
        var values = new double[data.Count];
        for (var i = 11; i < data.Count; i++)
        {
            decimal priorSum = 0,
                sum = 0;
            for (var j = i - 11; j < i - 1; j++)
                priorSum += Math.Abs((decimal)data.Closes[j] - (decimal)data.Opens[j]);
            for (var j = i - 10; j < i; j++)
                sum += Math.Abs((decimal)data.Closes[j] - (decimal)data.Opens[j]);
            var po = (decimal)data.Opens[i - 1];
            var pc = (decimal)data.Closes[i - 1];
            var o = (decimal)data.Opens[i];
            var c = (decimal)data.Closes[i];
            if (Math.Abs(pc - po) <= priorSum / 10)
                continue;
            var piercing = name == "PiercingLine";
            var match = piercing
                ? pc < po
                    && c >= o
                    && c - o > sum / 10
                    && o < (decimal)data.Lows[i - 1]
                    && c < po
                    && c > (po + pc) / 2
                : pc >= po
                    && c < o
                    && o > (decimal)data.Highs[i - 1]
                    && c > po
                    && c < pc - (pc - po) * (decimal)penetration;
            if (match)
                values[i] = piercing ? 100 : -100;
        }
        return new(Math.Min(11, data.Count), values);
    }

    // Each case resets the threshold history. Mirrors exercise both directions.
    internal static readonly (double Open, double Close)[] Candidates =
    [
        (-1, 5),
        (0, 5),
        (-1, 4),
        (-1, 4.125),
        (-1, 8),
        (-1, 7.875),
        (5, -1),
        (-1, 2),
        (-1, 4.5),
    ];

    internal static CompetitorData Fixture()
    {
        var bars = new List<(double O, double H, double L, double C)>();
        foreach (var c in Candidates)
        foreach (var mirror in new[] { false, true })
        {
            var block = Enumerable
                .Repeat((O: 4d, H: 10d, L: 0d, C: 6d), 10)
                .Append((8d, 10d, 0d, 0d))
                .Append(
                    (c.Open, Math.Max(c.Open, c.Close) + 1, Math.Min(c.Open, c.Close) - 1, c.Close)
                );
            bars.AddRange(block.Select(b => mirror ? (-b.Item1, -b.Item3, -b.Item2, -b.Item4) : b));
        }
        return CompetitorData.FromOhlc(
            bars.Select(b => b.O).ToArray(),
            bars.Select(b => b.H).ToArray(),
            bars.Select(b => b.L).ToArray(),
            bars.Select(b => b.C).ToArray()
        );
    }
}

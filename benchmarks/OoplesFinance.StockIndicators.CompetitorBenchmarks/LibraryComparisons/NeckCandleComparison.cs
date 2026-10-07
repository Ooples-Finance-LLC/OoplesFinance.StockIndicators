using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class NeckCandleComparison
{
    internal static readonly string[] Names = ["OnNeck", "InNeck", "Thrusting"];
    internal static readonly ComparisonPair[] Pairs = Names
        .Select(name => new ComparisonPair(
            "TaLib.Candles." + name,
            name + "Candle",
            (data, _) => Competitor(name, data),
            (data, _) => Ooples(name, data),
            (data, _) => Reference(name, data)
        ))
        .ToArray();

    internal static IIndicator Create(string name, int bodyPeriod = 10, int equalPeriod = 5) =>
        name switch
        {
            "OnNeck" => new OnNeckCandle(bodyPeriod, equalPeriod),
            "InNeck" => new InNeckCandle(bodyPeriod, equalPeriod),
            "Thrusting" => new ThrustingCandle(bodyPeriod, equalPeriod),
            _ => throw new ArgumentException("Unknown neck pattern", nameof(name)),
        };

    private static ComparisonSeries Ooples(string name, CompetitorData data)
    {
        var indicator = Create(name);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return new(Math.Min(11, data.Count), run[indicator.Outputs[0]].ToArray());
    }

    private static ComparisonSeries Competitor(string name, CompetitorData data)
    {
        var packed = new int[data.Count];
        System.Range range;
        var code = name switch
        {
            "OnNeck" => Candles.OnNeck<double>(
                data.Opens,
                data.Highs,
                data.Lows,
                data.Closes,
                System.Range.All,
                packed,
                out range
            ),
            "InNeck" => Candles.InNeck<double>(
                data.Opens,
                data.Highs,
                data.Lows,
                data.Closes,
                System.Range.All,
                packed,
                out range
            ),
            _ => Candles.Thrusting<double>(
                data.Opens,
                data.Highs,
                data.Lows,
                data.Closes,
                System.Range.All,
                packed,
                out range
            ),
        };
        if (data.Count == 1 && code == TALib.Core.RetCode.OutOfRangeParam)
            return new(1, [double.NaN]);
        if (code != TALib.Core.RetCode.Success)
            throw new InvalidOperationException("TA-Lib returned " + code);
        var first = Math.Min(11, data.Count);
        var (start, count) = range.GetOffsetAndLength(data.Count);
        if (count != data.Count - first || count > 0 && start != first)
            throw new InvalidOperationException("Unexpected neck alignment.");
        var values = new double[data.Count];
        for (var i = 0; i < count; i++)
            values[start + i] = packed[i];
        return new(first, values);
    }

    private static ComparisonSeries Reference(string name, CompetitorData data)
    {
        var values = new double[data.Count];
        for (var i = 11; i < data.Count; i++)
        {
            var po = (decimal)data.Opens[i - 1];
            var pc = (decimal)data.Closes[i - 1];
            var pl = (decimal)data.Lows[i - 1];
            var o = (decimal)data.Opens[i];
            var c = (decimal)data.Closes[i];
            if (pc >= po || c < o || o >= pl)
                continue;
            decimal body = 0,
                range = 0;
            for (var j = i - 11; j < i - 1; j++)
                body += Math.Abs((decimal)data.Closes[j] - (decimal)data.Opens[j]);
            for (var j = i - 6; j < i - 1; j++)
                range += (decimal)data.Highs[j] - (decimal)data.Lows[j];
            if (po - pc <= body / 10)
                continue;
            var tolerance = range / 100;
            var match = name switch
            {
                "OnNeck" => Math.Abs(c - pl) <= tolerance,
                "InNeck" => c >= pc && c <= pc + tolerance,
                _ => c > pc + tolerance && c <= (po + pc) / 2,
            };
            if (match)
                values[i] = -100;
        }
        return new(Math.Min(11, data.Count), values);
    }

    internal static readonly (double Open, double Close)[] Candidates =
    [
        (-2, -.625),
        (-2, -.5),
        (-2, 0),
        (-2, .5),
        (-2, .625),
        (-2, 2),
        (-2, 2.5),
        (-2, 2.625),
        (-2, 5),
        (-2, 5.125),
        (0, .5),
        (-.5, -.5),
        (-.125, -.25),
    ];

    internal static CompetitorData Fixture()
    {
        var bars = new List<(double O, double H, double L, double C)>();
        foreach (var c in Candidates)
        {
            bars.AddRange(Enumerable.Repeat((4d, 10d, 0d, 6d), 10));
            bars.Add((8, 10, 0, 2));
            bars.Add((c.Open, 10, -3, c.Close));
        }
        return CompetitorData.FromOhlc(
            bars.Select(b => b.O).ToArray(),
            bars.Select(b => b.H).ToArray(),
            bars.Select(b => b.L).ToArray(),
            bars.Select(b => b.C).ToArray()
        );
    }
}

using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class ContainmentCandleComparison
{
    internal static readonly string[] Names = ["Harami", "HaramiCross", "HomingPigeon", "DojiStar"];
    internal static readonly ComparisonPair[] Pairs = Names.Select(name => new ComparisonPair("TaLib.Candles." + name,
        name + "Candle", (data, _) => Competitor(name, data), (data, _) => Ooples(name, data), (data, _) => Reference(name, data))).ToArray();
    internal static IIndicator Create(string name, int period = 10) => name switch
    {
        "Harami" => new HaramiCandle(period), "HaramiCross" => new HaramiCrossCandle(period),
        "HomingPigeon" => new HomingPigeonCandle(period), "DojiStar" => new DojiStarCandle(period),
        _ => throw new ArgumentException("Unknown containment candle", nameof(name))
    };
    private static ComparisonSeries Ooples(string name, CompetitorData data)
    {
        var indicator = Create(name);
        using var run = new StockIndicatorBuilder().ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator).BuildAsync().GetAwaiter().GetResult();
        return new(Math.Min(11, data.Count), run[indicator.Outputs[0]].ToArray());
    }
    private static ComparisonSeries Competitor(string name, CompetitorData data)
    {
        var packed = new int[data.Count]; System.Range range;
        var code = name switch
        {
            "Harami" => Candles.Harami<double>(data.Opens, data.Highs, data.Lows, data.Closes, System.Range.All, packed, out range),
            "HaramiCross" => Candles.HaramiCross<double>(data.Opens, data.Highs, data.Lows, data.Closes, System.Range.All, packed, out range),
            "HomingPigeon" => Candles.HomingPigeon<double>(data.Opens, data.Highs, data.Lows, data.Closes, System.Range.All, packed, out range),
            "DojiStar" => Candles.DojiStar<double>(data.Opens, data.Highs, data.Lows, data.Closes, System.Range.All, packed, out range),
            _ => throw new ArgumentException("Unknown containment candle", nameof(name))
        };
        if (data.Count == 1 && code == TALib.Core.RetCode.OutOfRangeParam) return new(1, [double.NaN]);
        if (code != TALib.Core.RetCode.Success) throw new InvalidOperationException("TA-Lib returned " + code);
        var first = Math.Min(11, data.Count); var (start, count) = range.GetOffsetAndLength(data.Count);
        if (count != data.Count - first || count > 0 && start != first) throw new InvalidOperationException("Unexpected containment alignment.");
        var values = new double[data.Count];
        for (var i = 0; i < count; i++) values[start+i] = packed[i];
        return new(first, values);
    }
    private static ComparisonSeries Reference(string name, CompetitorData data)
    {
        var values = new double[data.Count];
        for (var i = 11; i < data.Count; i++)
        {
            decimal longSum = 0, shortSum = 0;
            var doji = name is "HaramiCross" or "DojiStar";
            for (var j = i-11; j < i-1; j++) longSum += Math.Abs((decimal)data.Closes[j] - (decimal)data.Opens[j]);
            for (var j = i-10; j < i; j++) shortSum += doji ? (decimal)data.Highs[j] - (decimal)data.Lows[j]
                : Math.Abs((decimal)data.Closes[j] - (decimal)data.Opens[j]);
            var previousBody = Math.Abs((decimal)data.Closes[i-1] - (decimal)data.Opens[i-1]);
            var body = Math.Abs((decimal)data.Closes[i] - (decimal)data.Opens[i]);
            if (previousBody <= longSum / 10 || body > shortSum / (doji ? 100 : 10)) continue;
            var priorTop = Math.Max(data.Opens[i-1], data.Closes[i-1]); var priorBottom = Math.Min(data.Opens[i-1], data.Closes[i-1]);
            var top = Math.Max(data.Opens[i], data.Closes[i]); var bottom = Math.Min(data.Opens[i], data.Closes[i]);
            var matches = name switch
            {
                "Harami" => top <= priorTop && bottom >= priorBottom,
                "HaramiCross" => top < priorTop && bottom > priorBottom,
                "HomingPigeon" => data.Closes[i-1] < data.Opens[i-1] && data.Closes[i] < data.Opens[i] && top < priorTop && bottom > priorBottom,
                _ => data.Closes[i-1] >= data.Opens[i-1] ? bottom > priorTop : top < priorBottom
            };
            if (matches) values[i] = name == "HomingPigeon" || data.Closes[i-1] < data.Opens[i-1] ? 100 : -100;
        }
        return new(Math.Min(11, data.Count), values);
    }
    internal static readonly (double PreviousOpen, double PreviousClose, double Open, double Close)[] Candidates =
    [
        (1, 8, 3, 4), (8, 1, 4, 3), (8, 1, 3, 4), (1, 8, 4, 3),
        (1, 8, 1, 2), (8, 1, 2, 1), (1, 8, 7, 8), (8, 1, 8, 7),
        (1, 8, 1, 8), (1, 8, 3, 5.5), (1, 8, 3, 5.625), (4, 6, 4.5, 5),
        (1, 8, 8.5, 9.5), (1, 8, 8, 9), (8, 1, -0.5, 0), (8, 1, 0, 1),
        (1, 8, 5, 5), (8, 1, 5, 5), (1, 8, 3, 4.125)
    ];
    internal static CompetitorData Fixture()
    {
        var bars = new List<(double Open, double High, double Low, double Close)>();
        foreach (var c in Candidates)
        {
            bars.AddRange(Enumerable.Repeat((4d, 10d, 0d, 6d), 10));
            bars.Add((c.PreviousOpen, 10, 0, c.PreviousClose));
            bars.Add((c.Open, 10, Math.Min(0, Math.Min(c.Open, c.Close)), c.Close));
        }
        return CompetitorData.FromOhlc(bars.Select(b => b.Open).ToArray(), bars.Select(b => b.High).ToArray(),
            bars.Select(b => b.Low).ToArray(), bars.Select(b => b.Close).ToArray());
    }
}

using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class ContextReversalComparison
{
    internal static readonly string[] Names = ["Hammer", "HangingMan", "InvertedHammer", "ShootingStar"];
    internal static readonly ComparisonPair[] Pairs = Names.Select(name => new ComparisonPair(
        "TaLib.Candles." + name, name + "Candle", (data, _) => Competitor(name, data),
        (data, _) => Ooples(name, data), (data, _) => Reference(name, data))).ToArray();

    internal static IIndicator Create(string name, int period = 10, int nearPeriod = 5) => name switch
    {
        "Hammer" => new HammerCandle(period, nearPeriod),
        "HangingMan" => new HangingManCandle(period, nearPeriod),
        "InvertedHammer" => new InvertedHammerCandle(period),
        "ShootingStar" => new ShootingStarCandle(period),
        _ => throw new ArgumentException("Unknown context reversal.", nameof(name))
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
        var packed = new int[data.Count];
        System.Range range;
        var code = name switch
        {
            "Hammer" => Candles.Hammer<double>(data.Opens, data.Highs, data.Lows, data.Closes, System.Range.All, packed, out range),
            "HangingMan" => Candles.HangingMan<double>(data.Opens, data.Highs, data.Lows, data.Closes, System.Range.All, packed, out range),
            "InvertedHammer" => Candles.InvertedHammer<double>(data.Opens, data.Highs, data.Lows, data.Closes, System.Range.All, packed, out range),
            "ShootingStar" => Candles.ShootingStar<double>(data.Opens, data.Highs, data.Lows, data.Closes, System.Range.All, packed, out range),
            _ => throw new ArgumentException("Unknown context reversal.", nameof(name))
        };
        if (data.Count == 1 && code == TALib.Core.RetCode.OutOfRangeParam) return new(1, [double.NaN]);
        if (code != TALib.Core.RetCode.Success) throw new InvalidOperationException("TA-Lib returned " + code);
        var first = Math.Min(11, data.Count);
        var (start, count) = range.GetOffsetAndLength(data.Count);
        if (count != data.Count - first || count > 0 && start != first) throw new InvalidOperationException("Unexpected reversal alignment.");
        var values = new double[data.Count];
        for (var i = 0; i < count; i++) values[start + i] = packed[i];
        return new(first, values);
    }
    private static ComparisonSeries Reference(string name, CompetitorData data)
    {
        var values = new double[data.Count];
        for (var i = 11; i < data.Count; i++)
        {
            decimal bodySum = 0, rangeSum = 0, nearSum = 0;
            for (var j = i - 10; j < i; j++)
            {
                bodySum += Math.Abs((decimal)data.Closes[j] - (decimal)data.Opens[j]);
                rangeSum += (decimal)data.Highs[j] - (decimal)data.Lows[j];
            }
            for (var j = i - 6; j < i - 1; j++) nearSum += (decimal)data.Highs[j] - (decimal)data.Lows[j];
            var top = Math.Max((decimal)data.Opens[i], (decimal)data.Closes[i]);
            var bottom = Math.Min((decimal)data.Opens[i], (decimal)data.Closes[i]);
            var body = top - bottom;
            var upper = (decimal)data.Highs[i] - top;
            var lower = bottom - (decimal)data.Lows[i];
            var lowerPattern = name is "Hammer" or "HangingMan";
            if (body >= bodySum / 10 || (lowerPattern ? lower : upper) <= body || (lowerPattern ? upper : lower) >= rangeSum / 100) continue;
            var context = name switch
            {
                "Hammer" => bottom <= (decimal)data.Lows[i - 1] + nearSum / 25,
                "HangingMan" => bottom >= (decimal)data.Highs[i - 1] - nearSum / 25,
                "InvertedHammer" => top < Math.Min((decimal)data.Opens[i - 1], (decimal)data.Closes[i - 1]),
                _ => bottom > Math.Max((decimal)data.Opens[i - 1], (decimal)data.Closes[i - 1])
            };
            if (context) values[i] = name is "Hammer" or "InvertedHammer" ? 100 : -100;
        }
        return new(Math.Min(11, data.Count), values);
    }

    internal static readonly (double Open, double High, double Low, double Close)[] Candidates =
    [
        (2, 3.5, 0, 3), // near previous low + 2, inclusive
        (2.125, 3.625, 0, 3.125), // just outside hammer near boundary
        (8, 9.5, 6, 9), // near previous high - 2, inclusive
        (7.875, 9.375, 5.875, 8.875), // just outside hanging-man boundary
        (2, 5, 1.5, 3), // strict downward body gap
        (3, 6, 2.5, 4), // touches previous body's bottom
        (7, 10, 6.5, 8), // strict upward body gap
        (6, 9, 5.5, 7), // touches previous body's top
        (2, 4, 0, 3), // upper shadow equals very-short threshold
        (2, 3.5, 1, 3), // lower shadow equals body
        (2, 4.5, -1, 4), // body equals prior mean
        (2, 2.5, 0, 2) // color-independent doji
    ];
    internal static CompetitorData Fixture()
    {
        var candles = new List<(double Open, double High, double Low, double Close)>();
        foreach (var candle in Candidates)
        {
            candles.AddRange(Enumerable.Repeat((4d, 10d, 0d, 6d), 11));
            candles.Add(candle);
            candles.AddRange(Enumerable.Repeat((4d, 10d, 0d, 6d), 11));
            candles.Add((candle.Close, candle.High, candle.Low, candle.Open));
        }
        return CompetitorData.FromOhlc(candles.Select(c => c.Open).ToArray(), candles.Select(c => c.High).ToArray(),
            candles.Select(c => c.Low).ToArray(), candles.Select(c => c.Close).ToArray());
    }
}

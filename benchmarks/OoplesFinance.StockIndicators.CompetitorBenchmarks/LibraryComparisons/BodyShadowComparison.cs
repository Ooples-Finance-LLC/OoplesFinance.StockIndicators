using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class BodyShadowComparison
{
    internal static readonly string[] Names = ["BeltHold", "Marubozu", "ClosingMarubozu", "SpinningTop", "HighWave", "LongLine", "ShortLine"];
    internal static readonly ComparisonPair[] Pairs = Names.Select(name => new ComparisonPair(
        "TaLib.Candles." + name, name + "Candle", (data, _) => Competitor(name, data),
        (data, _) => Ooples(name, data), (data, _) => Reference(name, data))).ToArray();

    internal static IIndicator Create(string name, int period = 10) => name switch
    {
        "BeltHold" => new BeltHoldCandle(period),
        "Marubozu" => new MarubozuCandle(period),
        "ClosingMarubozu" => new ClosingMarubozuCandle(period),
        "SpinningTop" => new SpinningTopCandle(period),
        "HighWave" => new HighWaveCandle(period),
        "LongLine" => new LongLineCandle(period),
        "ShortLine" => new ShortLineCandle(period),
        _ => throw new ArgumentException("Unknown body/shadow pattern.", nameof(name))
    };

    private static ComparisonSeries Ooples(string name, CompetitorData data)
    {
        var indicator = Create(name);
        using var run = new StockIndicatorBuilder().ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator).BuildAsync().GetAwaiter().GetResult();
        return new(Math.Min(10, data.Count), run[indicator.Outputs[0]].ToArray());
    }

    private static ComparisonSeries Competitor(string name, CompetitorData data)
    {
        var packed = new int[data.Count];
        System.Range range;
        var code = name switch
        {
            "BeltHold" => Candles.BeltHold<double>(data.Opens, data.Highs, data.Lows, data.Closes, System.Range.All, packed, out range),
            "Marubozu" => Candles.Marubozu<double>(data.Opens, data.Highs, data.Lows, data.Closes, System.Range.All, packed, out range),
            "ClosingMarubozu" => Candles.ClosingMarubozu<double>(data.Opens, data.Highs, data.Lows, data.Closes, System.Range.All, packed, out range),
            "SpinningTop" => Candles.SpinningTop<double>(data.Opens, data.Highs, data.Lows, data.Closes, System.Range.All, packed, out range),
            "HighWave" => Candles.HighWave<double>(data.Opens, data.Highs, data.Lows, data.Closes, System.Range.All, packed, out range),
            "LongLine" => Candles.LongLine<double>(data.Opens, data.Highs, data.Lows, data.Closes, System.Range.All, packed, out range),
            "ShortLine" => Candles.ShortLine<double>(data.Opens, data.Highs, data.Lows, data.Closes, System.Range.All, packed, out range),
            _ => throw new ArgumentException("Unknown body/shadow pattern.", nameof(name))
        };
        if (data.Count == 1 && code == TALib.Core.RetCode.OutOfRangeParam) return new(1, [double.NaN]);
        if (code != TALib.Core.RetCode.Success) throw new InvalidOperationException("TA-Lib returned " + code);
        var first = Math.Min(10, data.Count);
        var (start, count) = range.GetOffsetAndLength(data.Count);
        if (count != data.Count - first || count > 0 && start != first)
            throw new InvalidOperationException("Unexpected body/shadow pattern alignment.");
        var values = new double[data.Count];
        for (var i = 0; i < count; i++) values[start + i] = packed[i];
        return new(first, values);
    }

    private static ComparisonSeries Reference(string name, CompetitorData data)
    {
        var values = new double[data.Count];
        for (var i = 10; i < data.Count; i++)
        {
            decimal bodyTotal = 0, rangeTotal = 0, shadowsTotal = 0;
            for (var j = i - 10; j < i; j++)
            {
                var previousBody = Math.Abs((decimal)data.Closes[j] - (decimal)data.Opens[j]);
                var range = (decimal)data.Highs[j] - (decimal)data.Lows[j];
                bodyTotal += previousBody; rangeTotal += range; shadowsTotal += range - previousBody;
            }
            var body = Math.Abs((decimal)data.Closes[i] - (decimal)data.Opens[i]);
            var upper = (decimal)data.Highs[i] - Math.Max((decimal)data.Closes[i], (decimal)data.Opens[i]);
            var lower = Math.Min((decimal)data.Closes[i], (decimal)data.Opens[i]) - (decimal)data.Lows[i];
            var longBody = name is "BeltHold" or "Marubozu" or "ClosingMarubozu" or "LongLine";
            var bodyMatches = longBody ? body > bodyTotal / 10 : body < bodyTotal / 10;
            var shadows = name switch
            {
                "Marubozu" => upper < rangeTotal / 100 && lower < rangeTotal / 100,
                "ClosingMarubozu" => (data.Closes[i] >= data.Opens[i] ? upper : lower) < rangeTotal / 100,
                "BeltHold" => (data.Closes[i] >= data.Opens[i] ? lower : upper) < rangeTotal / 100,
                "SpinningTop" => upper > body && lower > body,
                "HighWave" => upper > 2 * body && lower > 2 * body,
                _ => upper < shadowsTotal / 20 && lower < shadowsTotal / 20
            };
            if (bodyMatches && shadows) values[i] = data.Closes[i] >= data.Opens[i] ? 100 : -100;
        }
        return new(Math.Min(10, data.Count), values);
    }

    internal static readonly (double Open, double High, double Low, double Close)[] Candidates =
    [
        (1, 5, .5, 4.5), // long body, very short shadows
        (1, 5.5, 0, 4.5), // very-short shadow equality
        (4, 6.5, 0, 6), // body equal to prior mean
        (4, 8.5, 0, 8), // only closing end is short
        (1, 8, .5, 4.5), // only opening end is short
        (4, 8, 1, 5), // short body, shadows longer than twice body
        (4, 7, 2, 5), // high-wave shadow equality
        (4, 6, 3, 5), // spinning-top shadow equality
        (4, 6, 3.5, 5), // short line
        (4, 9, 0, 5), // short-shadow equality
        (1, 7, 0, 4), // long line
        (4, 5, 3, 4), // doji can qualify as bullish
        (0, 0, 0, 0)
    ];

    internal static CompetitorData Fixture()
    {
        var candles = new List<(double Open, double High, double Low, double Close)>();
        foreach (var candle in Candidates)
        {
            candles.AddRange(Enumerable.Repeat((4d, 10d, 0d, 6d), 10));
            candles.Add(candle);
            candles.AddRange(Enumerable.Repeat((4d, 10d, 0d, 6d), 10));
            candles.Add((candle.Close, candle.High, candle.Low, candle.Open));
        }
        return CompetitorData.FromOhlc(candles.Select(c => c.Open).ToArray(), candles.Select(c => c.High).ToArray(),
            candles.Select(c => c.Low).ToArray(), candles.Select(c => c.Close).ToArray());
    }
}

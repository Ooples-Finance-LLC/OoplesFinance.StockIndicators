using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TC = Trady.Analysis.Candlestick;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class PercentileCandleComparison
{
    internal static readonly string[] Names = ["LongDay", "ShortDay", "BullishLongDay", "BearishLongDay", "BullishShortDay", "BearishShortDay", "LongUpperShadow", "LongLowerShadow"];
    internal static bool IgnoresParameters(string name) => name is "BearishLongDay" or "BullishShortDay" or "BearishShortDay";
    internal static readonly ComparisonPair[] Pairs = Names.Select(name => new ComparisonPair("Trady.Candlestick." + name,
        Create(name, 20).GetType().Name, (data, period) => Competitor(name, data, period),
        (data, period) => Ooples(name, data, period), (data, period) => Reference(name, data, period))).ToArray();
    internal static IIndicator Create(string name, int period, decimal? percentile = null) => name switch
    {
        "LongDay" => new LongBodyCandle(period, percentile ?? 0.75m),
        "ShortDay" => new ShortBodyCandle(period, percentile ?? 0.25m),
        "BullishLongDay" => new BullishLongBodyCandle(period, percentile ?? 0.75m),
        "BearishLongDay" => new BearishLongBodyCandle(period, percentile ?? 0.75m),
        "BullishShortDay" => new BullishShortBodyCandle(period, percentile ?? 0.25m),
        "BearishShortDay" => new BearishShortBodyCandle(period, percentile ?? 0.25m),
        "LongUpperShadow" => new UpperShadowAtOrAbovePercentileCandle(period, percentile ?? 0.75m),
        "LongLowerShadow" => new LowerShadowBelowPercentileCandle(period, percentile ?? 0.25m),
        _ => throw new ArgumentException("Unknown percentile candle", nameof(name))
    };
    private static ComparisonSeries Ooples(string name, CompetitorData data, int period)
    {
        var indicator = Create(name, IgnoresParameters(name) ? 20 : period);
        using var run = new StockIndicatorBuilder().ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator).BuildAsync().GetAwaiter().GetResult();
        return new(0, run[indicator.Outputs[0]].ToArray());
    }
    internal static ComparisonSeries Competitor(string name, CompetitorData data, int period, decimal? percentile = null)
    {
        var q = percentile ?? (name.Contains("Short", StringComparison.Ordinal) || name == "LongLowerShadow" ? 0.25m : 0.75m);
        var values = name switch
        {
            "LongDay" => new TC.LongDay(data.Candles, period, q).Compute().Select(r => r.Tick == true ? 1d : 0d).ToArray(),
            "ShortDay" => new TC.ShortDay(data.Candles, period, q).Compute().Select(r => r.Tick == true ? 1d : 0d).ToArray(),
            "BullishLongDay" => new TC.BullishLongDay(data.Candles, period, q).Compute().Select(r => r.Tick == true ? 1d : 0d).ToArray(),
            "BearishLongDay" => new TC.BearishLongDay(data.Candles, period, q).Compute().Select(r => r.Tick == true ? 1d : 0d).ToArray(),
            "BullishShortDay" => new TC.BullishShortDay(data.Candles, period, q).Compute().Select(r => r.Tick == true ? 1d : 0d).ToArray(),
            "BearishShortDay" => new TC.BearishShortDay(data.Candles, period, q).Compute().Select(r => r.Tick == true ? 1d : 0d).ToArray(),
            "LongUpperShadow" => new TC.LongUpperShadow(data.Candles, period, q).Compute().Select(r => r.Tick == true ? 1d : 0d).ToArray(),
            "LongLowerShadow" => new TC.LongLowerShadow(data.Candles, period, q).Compute().Select(r => r.Tick == true ? 1d : 0d).ToArray(),
            _ => throw new ArgumentException("Unknown percentile candle", nameof(name))
        };
        return new(0, values);
    }
    private static ComparisonSeries Reference(string name, CompetitorData data, int requestedPeriod)
    {
        var period = IgnoresParameters(name) ? 20 : requestedPeriod;
        var above = !name.Contains("Short", StringComparison.Ordinal) && name != "LongLowerShadow";
        var q = above ? 0.75m : 0.25m;
        var lengths = Enumerable.Range(0, data.Count).Select(i => name switch
        {
            "LongUpperShadow" => (decimal)data.Highs[i] - Math.Max((decimal)data.Opens[i], (decimal)data.Closes[i]),
            "LongLowerShadow" => Math.Min((decimal)data.Opens[i], (decimal)data.Closes[i]) - (decimal)data.Lows[i],
            _ => Math.Abs((decimal)data.Closes[i] - (decimal)data.Opens[i])
        }).ToArray();
        var values = new double[data.Count];
        for (var i = period - 1; i < data.Count; i++)
        {
            var window = lengths.Skip(i - period + 1).Take(period).Order().ToArray();
            var position = q * (period - 1); var lower = (int)position;
            var threshold = window[lower];
            if (lower + 1 < period) threshold += (window[lower + 1] - threshold) * (position - lower);
            var color = name.StartsWith("Bullish", StringComparison.Ordinal) ? data.Closes[i] > data.Opens[i]
                : !name.StartsWith("Bearish", StringComparison.Ordinal) || data.Closes[i] < data.Opens[i];
            values[i] = color && (lengths[i] >= threshold) == above ? 1 : 0;
        }
        return new(0, values);
    }
    internal static CompetitorData Fixture()
    {
        double[] lengths = [1, 2, 3, 4, 4, 0, 8, 1, 2, 6, 1, 3];
        var closes = Enumerable.Range(0, 80).Select(i => 20 + (i%2 == 0 ? 1 : -1) * lengths[i%lengths.Length]).ToArray();
        var opens = Enumerable.Repeat(20d, closes.Length).ToArray();
        var highs = Enumerable.Range(0, closes.Length).Select(i => Math.Max(opens[i], closes[i]) + lengths[(i+3)%lengths.Length]).ToArray();
        var lows = Enumerable.Range(0, closes.Length).Select(i => Math.Min(opens[i], closes[i]) - lengths[(i+5)%lengths.Length]).ToArray();
        return CompetitorData.FromOhlc(opens, highs, lows, closes);
    }
}

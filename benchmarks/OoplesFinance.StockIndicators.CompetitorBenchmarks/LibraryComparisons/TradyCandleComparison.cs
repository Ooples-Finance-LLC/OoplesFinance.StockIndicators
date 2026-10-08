using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TC = Trady.Analysis.Candlestick;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class TradyCandleComparison
{
    internal static readonly string[] Names = ["DragonflyDoji", "DragonifyDoji", "GravestoneDoji", "Doji", "Bullish", "Bearish", "UpTrend", "DownTrend", "BullishEngulfingPattern", "BearishEngulfingPattern"];
    internal static readonly ComparisonPair[] Pairs = Names.Select(name => new ComparisonPair(
        "Trady.Candlestick." + name, name switch { "DragonflyDoji" or "DragonifyDoji" => "DragonflyDojiCandle", "GravestoneDoji" => "GravestoneDojiCandle", "Doji" => "DojiCandle", "Bullish" => "BullishCandle", "Bearish" => "BearishCandle", "UpTrend" => "CandleUpTrend", "DownTrend" => "CandleDownTrend", _ => name },
        (data, period) => Competitor(name, data, period), (data, period) => Ooples(name, data, period),
        (data, period) => Reference(name, data, period))).ToArray();

    internal static IIndicator Create(string name, int period) => name switch
    {
        "DragonflyDoji" or "DragonifyDoji" => new DragonflyDojiCandle(),
        "GravestoneDoji" => new GravestoneDojiCandle(),
        "Doji" => new DojiCandle(),
        "Bullish" => new BullishCandle(),
        "Bearish" => new BearishCandle(),
        "UpTrend" => new CandleUpTrend(period),
        "DownTrend" => new CandleDownTrend(period),
        "BullishEngulfingPattern" => new BullishEngulfingPattern(period),
        "BearishEngulfingPattern" => new BearishEngulfingPattern(period),
        _ => throw new ArgumentException("Unknown candle pattern.", nameof(name))
    };

    private static int First(string name, int count, int period) => Math.Min(count,
        name is "UpTrend" or "DownTrend" ? period : name.EndsWith("EngulfingPattern", StringComparison.Ordinal) ? 1 : 0);

    private static ComparisonSeries Ooples(string name, CompetitorData data, int period)
    {
        var indicator = Create(name, period);
        using var run = new StockIndicatorBuilder().ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator).BuildAsync().GetAwaiter().GetResult();
        return new(First(name, data.Count, period), run[indicator.Outputs[0]].ToArray());
    }

    private static ComparisonSeries Competitor(string name, CompetitorData data, int period)
    {
        double[] values = name switch
        {
            "DragonflyDoji" => new TC.DragonflyDoji<Trady.Core.Candle, Trady.Analysis.AnalyzableTick<bool>>(
                data.Candles, candle => (candle.Open, candle.High, candle.Low, candle.Close)).Compute(null, null).Select(row => row.Tick ? 1d : 0d).ToArray(),
            "DragonifyDoji" => new TC.DragonifyDoji(data.Candles).Compute(null, null).Select(row => row.Tick ? 1d : 0d).ToArray(),
            "GravestoneDoji" => new TC.GravestoneDoji(data.Candles).Compute(null, null).Select(row => row.Tick ? 1d : 0d).ToArray(),
            "Doji" => new TC.Doji(data.Candles).Compute(null, null).Select(row => row.Tick ? 1d : 0d).ToArray(),
            "Bullish" => new TC.Bullish(data.Candles).Compute(null, null).Select(row => row.Tick ? 1d : 0d).ToArray(),
            "Bearish" => new TC.Bearish(data.Candles).Compute(null, null).Select(row => row.Tick ? 1d : 0d).ToArray(),
            "UpTrend" => new TC.UpTrend(data.Candles, period).Compute(null, null).Select(row => Value(row.Tick)).ToArray(),
            "DownTrend" => new TC.DownTrend(data.Candles, period).Compute(null, null).Select(row => Value(row.Tick)).ToArray(),
            "BullishEngulfingPattern" => new TC.BullishEngulfingPattern(data.Candles, period).Compute(null, null).Select(row => Value(row.Tick)).ToArray(),
            "BearishEngulfingPattern" => new TC.BearishEngulfingPattern(data.Candles, period).Compute(null, null).Select(row => Value(row.Tick)).ToArray(),
            _ => throw new ArgumentException("Unknown candle pattern.", nameof(name))
        };
        return new(First(name, data.Count, period), values);
        static double Value(bool? value) => value.HasValue ? value.Value ? 1 : 0 : double.NaN;
    }

    private static ComparisonSeries Reference(string name, CompetitorData data, int period)
    {
        var values = new double[data.Count];
        for (var i = 0; i < data.Count; i++)
        {
            if (name is "DragonflyDoji" or "DragonifyDoji" or "GravestoneDoji")
            {
                var body = Math.Abs((decimal)data.Closes[i] - (decimal)data.Opens[i]);
                var range = (decimal)data.Highs[i] - (decimal)data.Lows[i];
                var midpoint = ((decimal)data.Closes[i] + (decimal)data.Opens[i]) / 2;
                var distance = name == "GravestoneDoji" ? midpoint - (decimal)data.Lows[i] : (decimal)data.Highs[i] - midpoint;
                values[i] = body < 0.1m * range && distance < 0.1m * range ? 1 : 0;
                continue;
            }
            if (name == "Doji")
            {
                values[i] = Math.Abs((decimal)data.Closes[i] - (decimal)data.Opens[i]) <
                    0.1m * ((decimal)data.Highs[i] - (decimal)data.Lows[i]) ? 1 : 0;
                continue;
            }
            if (name == "Bullish") { values[i] = data.Closes[i] > data.Opens[i] ? 1 : 0; continue; }
            if (name == "Bearish") { values[i] = data.Closes[i] < data.Opens[i] ? 1 : 0; continue; }
            var engulfing = name.EndsWith("EngulfingPattern", StringComparison.Ordinal);
            var end = engulfing ? i - 1 : i;
            if (end < period) continue;
            var rising = name is "UpTrend" or "BearishEngulfingPattern";
            var trend = Enumerable.Range(end - period, period).All(j => rising
                ? data.Highs[j + 1] > data.Highs[j] && data.Lows[j + 1] > data.Lows[j]
                : data.Highs[j + 1] < data.Highs[j] && data.Lows[j + 1] < data.Lows[j]);
            if (engulfing)
            {
                var previousBody = (decimal)data.Closes[i - 1] - (decimal)data.Opens[i - 1];
                var body = (decimal)data.Closes[i] - (decimal)data.Opens[i];
                trend &= rising ? previousBody > 0 && body < 0 : previousBody < 0 && body > 0;
                trend &= Math.Min(data.Opens[i], data.Closes[i]) < Math.Min(data.Opens[i - 1], data.Closes[i - 1]) &&
                    Math.Max(data.Opens[i], data.Closes[i]) > Math.Max(data.Opens[i - 1], data.Closes[i - 1]);
            }
            values[i] = trend ? 1 : 0;
        }
        return new(First(name, data.Count, period), values);
    }

    internal static CompetitorData TrendFixture(int period)
    {
        var opens = new List<double>();
        var closes = new List<double>();
        foreach (var rising in new[] { false, true })
        foreach (var boundary in new[] { false, true })
        {
            for (var i = 0; i <= period; i++)
            {
                var price = rising ? 10 + i * 2 : 100 - i * 2;
                opens.Add(price);
                closes.Add(price + (rising ? 1 : -1));
            }
            var previousOpen = opens[^1];
            var previousClose = closes[^1];
            opens.Add(previousClose + (boundary ? 0 : rising ? 1 : -1));
            closes.Add(previousOpen + (rising ? -1 : 1));
        }
        return CompetitorData.FromBodies(opens.ToArray(), closes.ToArray());
    }
}

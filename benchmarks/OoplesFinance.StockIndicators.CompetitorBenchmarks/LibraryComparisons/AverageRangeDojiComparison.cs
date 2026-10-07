using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class AverageRangeDojiComparison
{
    internal static readonly string[] Names = ["TakuriLine", "Doji", "DragonflyDoji", "GravestoneDoji", "LongLeggedDoji"];
    internal static readonly ComparisonPair[] Pairs = Names.Select(name => new ComparisonPair(
        "TaLib.Candles." + name, name == "TakuriLine" ? "TakuriLineCandle" : "AverageRange" + name,
        (data, _) => Competitor(name, data), (data, _) => Ooples(name, data),
        (data, _) => Reference(name, data))).ToArray();

    internal static IIndicator Create(string name, int period = 10) => name switch
    {
        "TakuriLine" => new TakuriLineCandle(period),
        "Doji" => new AverageRangeDoji(period),
        "DragonflyDoji" => new AverageRangeDragonflyDoji(period),
        "GravestoneDoji" => new AverageRangeGravestoneDoji(period),
        "LongLeggedDoji" => new AverageRangeLongLeggedDoji(period),
        _ => throw new ArgumentException("Unknown doji pattern.", nameof(name))
    };

    private static int Warmup(string name) => name == "DragonflyDoji" ? 11 : 10;

    private static ComparisonSeries Ooples(string name, CompetitorData data)
    {
        var indicator = Create(name);
        using var run = new StockIndicatorBuilder().ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator).BuildAsync().GetAwaiter().GetResult();
        return new(Math.Min(Warmup(name), data.Count), run[indicator.Outputs[0]].ToArray());
    }

    private static ComparisonSeries Competitor(string name, CompetitorData data)
    {
        var packed = new int[data.Count];
        System.Range range;
        var code = name switch
        {
            "TakuriLine" => Candles.TakuriLine<double>(data.Opens, data.Highs, data.Lows, data.Closes, System.Range.All, packed, out range),
            "Doji" => Candles.Doji<double>(data.Opens, data.Highs, data.Lows, data.Closes, System.Range.All, packed, out range),
            "DragonflyDoji" => Candles.DragonflyDoji<double>(data.Opens, data.Highs, data.Lows, data.Closes, System.Range.All, packed, out range),
            "GravestoneDoji" => Candles.GravestoneDoji<double>(data.Opens, data.Highs, data.Lows, data.Closes, System.Range.All, packed, out range),
            _ => Candles.LongLeggedDoji<double>(data.Opens, data.Highs, data.Lows, data.Closes, System.Range.All, packed, out range)
        };
        if (data.Count == 1 && code == TALib.Core.RetCode.OutOfRangeParam) return new(1, [double.NaN]);
        if (code != TALib.Core.RetCode.Success) throw new InvalidOperationException("TA-Lib returned " + code);
        var first = Math.Min(Warmup(name), data.Count);
        var (start, count) = range.GetOffsetAndLength(data.Count);
        if (count != data.Count - first || count > 0 && start != first)
            throw new InvalidOperationException("Unexpected average-range doji alignment.");
        var values = new double[data.Count];
        for (var i = 0; i < count; i++) values[start + i] = packed[i];
        return new(first, values);
    }

    private static ComparisonSeries Reference(string name, CompetitorData data)
    {
        var values = new double[data.Count];
        for (var i = Warmup(name); i < data.Count; i++)
        {
            decimal sum = 0;
            for (var j = i - 10; j < i; j++) sum += (decimal)data.Highs[j] - (decimal)data.Lows[j];
            var limit = sum / 100m;
            var body = Math.Abs((decimal)data.Closes[i] - (decimal)data.Opens[i]);
            var upper = (decimal)data.Highs[i] - Math.Max((decimal)data.Closes[i], (decimal)data.Opens[i]);
            var lower = Math.Min((decimal)data.Closes[i], (decimal)data.Opens[i]) - (decimal)data.Lows[i];
            var shadows = name switch
            {
                "DragonflyDoji" => upper < limit && lower > limit,
                "GravestoneDoji" => lower < limit && upper > limit,
                "TakuriLine" => upper < limit && lower > 2 * body,
                "LongLeggedDoji" => upper > body || lower > body,
                _ => true
            };
            values[i] = body <= limit && shadows ? 100 : 0;
        }
        return new(Math.Min(Warmup(name), data.Count), values);
    }

    internal static CompetitorData Fixture()
    {
        var opens = Enumerable.Repeat(5d, 11).Concat(new[] { 9.5, .5, 9, 1, 9, 0, 8.75, 0, 5, 0 }).ToArray();
        var closes = Enumerable.Repeat(5d, 11).Concat(new[] { 9.5, .5, 9, 1, 10, 1, 10, 1.25, 5, 0 }).ToArray();
        return CompetitorData.FromOhlc(opens, Enumerable.Repeat(10d, opens.Length).ToArray(), new double[opens.Length], closes);
    }
}

using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class CandleComparison
{
    internal static ComparisonSeries Ooples(CompetitorData data, int unused)
    {
        var indicator = new EngulfingPattern();
        using var run = new StockIndicatorBuilder().ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator).BuildAsync().GetAwaiter().GetResult();
        return new(Math.Min(2, data.Count), run[indicator].ToArray());
    }

    internal static ComparisonSeries TaLib(CompetitorData data, int unused)
    {
        var packed = new int[data.Count];
        var code = Candles.Engulfing<double>(data.Opens, data.Highs, data.Lows, data.Closes,
            System.Range.All, packed, out var range);
        // Version 0.5.0 rejects a one-bar input before checking lookback. There are
        // no mature values to compare; require that documented adapter status explicitly.
        if (data.Count == 1 && code == TALib.Core.RetCode.OutOfRangeParam)
            return new(1, [double.NaN]);
        if (code != TALib.Core.RetCode.Success) throw new InvalidOperationException("TA-Lib returned " + code);
        var first = Math.Min(2, data.Count);
        var (start, count) = range.GetOffsetAndLength(data.Count);
        if (count != data.Count - first || (count > 0 && start != first))
            throw new InvalidOperationException("Unexpected engulfing alignment.");
        var values = new double[data.Count];
        for (var i = 0; i < count; i++) values[start + i] = packed[i];
        return new(first, values);
    }

    internal static ComparisonSeries Reference(CompetitorData data)
    {
        var values = new double[data.Count];
        for (var i = 2; i < data.Count; i++)
        {
            var currentUp = data.Closes[i] >= data.Opens[i];
            var previousUp = data.Closes[i - 1] >= data.Opens[i - 1];
            if (currentUp == previousUp) continue;
            var left = Math.Min(data.Closes[i], data.Opens[i]);
            var right = Math.Max(data.Closes[i], data.Opens[i]);
            var previousLeft = Math.Min(data.Closes[i - 1], data.Opens[i - 1]);
            var previousRight = Math.Max(data.Closes[i - 1], data.Opens[i - 1]);
            if (left <= previousLeft && right >= previousRight && (left < previousLeft || right > previousRight))
                values[i] = currentUp ? 100 : -100;
        }
        return new(Math.Min(2, data.Count), values);
    }

    internal static CompetitorData ShadowFixture() => CompetitorData.FromOhlc(
        [9.5, 0.5, 9, 1, 9.25, 0.25, 8.75, 0, 5, 0],
        Enumerable.Repeat(10d, 10).ToArray(), new double[10],
        [9.5, 0.5, 9, 1, 9.75, 0.75, 10, 1.25, 5, 0]);

    internal static CompetitorData Fixture(int repetitions = 1)
    {
        // Exhaust every pair of body endpoints, including equal bodies and dojis.
        var opens = new List<double>();
        var closes = new List<double>();
        for (var repeat = 0; repeat < repetitions; repeat++)
        for (var a = 1; a <= 4; a++)
        for (var b = 1; b <= 4; b++)
        for (var c = 1; c <= 4; c++)
        for (var d = 1; d <= 4; d++)
        {
            opens.AddRange([a, c]);
            closes.AddRange([b, d]);
        }
        return CompetitorData.FromOhlc(opens.ToArray(), opens.Zip(closes, (open, close) => Math.Max(open, close) + 5).ToArray(),
            opens.Zip(closes, (open, close) => Math.Min(open, close) - 5).ToArray(), closes.ToArray());
    }
}

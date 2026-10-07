using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Trady.Analysis;
using Trady.Core.Infrastructure;
using T = Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class TradyExtremaComparison
{
    internal static readonly string[] Names = ["Highest", "Lowest", "HighestClose", "LowestClose", "HighestHigh", "LowestLow",
        "HistoricalHighest", "HistoricalLowest", "HistoricalHighestClose", "HistoricalLowestClose", "HistoricalHighestHigh", "HistoricalLowestLow"];
    internal static readonly ComparisonPair[] Pairs = Names.Select(name => new ComparisonPair("Trady.Indicator." + name,
        name.Contains("Highest", StringComparison.Ordinal) ? "HighestHigh" : "LowestLow",
        (data, period) => Competitor(name, data, period), (data, period) => Ooples(name, data, period),
        (data, period) => Reference(name, data, period))).ToArray();
    private static bool Historical(string name) => name.StartsWith("Historical", StringComparison.Ordinal);
    private static bool Ohlc(string name) => name.EndsWith("HighestHigh", StringComparison.Ordinal) || name.EndsWith("LowestLow", StringComparison.Ordinal);
    private static int First(string name, int count, int period) => Historical(name) ? 0 : Math.Min(period - 1, count);

    private static ComparisonSeries Ooples(string name, CompetitorData data, int period)
    {
        // A window as long as the complete input preserves the expanding prefix at every bar.
        var length = Historical(name) ? data.Count : period;
        IIndicator indicator = name.Contains("Highest", StringComparison.Ordinal) ? new HighestHigh(length) : new LowestLow(length);
        using var run = new StockIndicatorBuilder().ConfigureSource(Bars.From(Ohlc(name) ? data.IndicatorBars : data.CloseBars))
            .ConfigureIndicators(indicator).BuildAsync().GetAwaiter().GetResult();
        return new(First(name, data.Count, period), run[indicator.Outputs[0]].ToArray());
    }
    private static ComparisonSeries Competitor(string name, CompetitorData data, int period)
    {
        var rows = name switch
        {
            "Highest" => new T.Highest<IOhlcv, AnalyzableTick<decimal?>>(data.Candles, c => c.Close, period).Compute(),
            "Lowest" => new T.Lowest<IOhlcv, AnalyzableTick<decimal?>>(data.Candles, c => c.Close, period).Compute(),
            "HighestClose" => new T.HighestClose(data.Candles, period).Compute(),
            "LowestClose" => new T.LowestClose(data.Candles, period).Compute(),
            "HighestHigh" => new T.HighestHigh(data.Candles, period).Compute(),
            "LowestLow" => new T.LowestLow(data.Candles, period).Compute(),
            "HistoricalHighest" => new T.HistoricalHighest<IOhlcv, AnalyzableTick<decimal?>>(data.Candles, c => c.Close).Compute(),
            "HistoricalLowest" => new T.HistoricalLowest<IOhlcv, AnalyzableTick<decimal?>>(data.Candles, c => c.Close).Compute(),
            "HistoricalHighestClose" => new T.HistoricalHighestClose(data.Candles).Compute(),
            "HistoricalLowestClose" => new T.HistoricalLowestClose(data.Candles).Compute(),
            "HistoricalHighestHigh" => new T.HistoricalHighestHigh(data.Candles).Compute(),
            "HistoricalLowestLow" => new T.HistoricalLowestLow(data.Candles).Compute(),
            _ => throw new ArgumentException("Unknown extrema.", nameof(name))
        };
        return new(First(name, data.Count, period), rows.Select(row => (double?)row.Tick ?? double.NaN).ToArray());
    }
    private static ComparisonSeries Reference(string name, CompetitorData data, int period)
    {
        var maximum = name.Contains("Highest", StringComparison.Ordinal);
        var input = Ohlc(name) ? maximum ? data.Highs : data.Lows : data.Closes;
        var values = Enumerable.Repeat(double.NaN, data.Count).ToArray();
        var first = First(name, data.Count, period);
        if (Historical(name))
        {
            var extreme = input[0];
            for (var i = 0; i < input.Length; i++)
            {
                extreme = maximum ? Math.Max(extreme, input[i]) : Math.Min(extreme, input[i]);
                values[i] = extreme;
            }
        }
        else for (var i = first; i < input.Length; i++)
        {
            var sorted = input.AsSpan(i - period + 1, period).ToArray();
            Array.Sort(sorted);
            values[i] = maximum ? sorted[^1] : sorted[0];
        }
        return new(first, values);
    }
    internal static CompetitorData Fixture() => CompetitorData.FromOhlc(
        [5, 4, 7, 3, 5, 6, 2, 1], [9, 8, 10, 11, 9, 9, 7, 5],
        [2, 1, 3, -1, 0, 2, -2, -3], [6, 5, 8, 4, 6, 7, 3, 2]);
}

using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class ExtremaComparison
{
    internal static readonly ComparisonPair[] Pairs = new[] { "Min", "Max", "MinMax" }.Select(name => new ComparisonPair(
        "TaLib.Functions." + name, name == "Min" ? "LowestLow" : name == "Max" ? "HighestHigh" : "LowestLow + HighestHigh",
        (data, period) => Competitor(name, data, period), (data, period) => Ooples(name, data, period),
        (data, period) => Reference(name, data, period), Outputs(name))).ToArray();

    private static string[] Outputs(string name) => name == "MinMax" ? ["Min", "Max"] : [name];

    private static ComparisonSeries Ooples(string name, CompetitorData data, int period)
    {
        var indicators = Outputs(name).ToDictionary(key => key, key => key == "Min"
            ? (IIndicator)new LowestLow(period) : new HighestHigh(period), StringComparer.Ordinal);
        // TA-Lib extrema consume one scalar stream. Prepare identical OHLC bars from that
        // stream outside timing, then use the library's existing high/low window indicators.
        using var run = new StockIndicatorBuilder().ConfigureSource(Bars.From(data.CloseBars))
            .ConfigureIndicators(indicators.Values.ToArray()).BuildAsync().GetAwaiter().GetResult();
        return new(indicators.ToDictionary(pair => pair.Key,
            pair => new ComparisonOutput(Math.Min(period - 1, data.Count), run[pair.Value.Outputs[0]].ToArray()), StringComparer.Ordinal));
    }

    private static ComparisonSeries Competitor(string name, CompetitorData data, int period)
    {
        var minimum = name != "Max" ? new double[data.Count] : [];
        var maximum = name != "Min" ? new double[data.Count] : [];
        System.Range range;
        var code = name switch
        {
            "Min" => Functions.Min<double>(data.Closes, System.Range.All, minimum, out range, period),
            "Max" => Functions.Max<double>(data.Closes, System.Range.All, maximum, out range, period),
            _ => Functions.MinMax<double>(data.Closes, System.Range.All, minimum, maximum, out range, period)
        };
        var first = Math.Min(period - 1, data.Count);
        var oneBarRejected = data.Count == 1 && code == TALib.Core.RetCode.OutOfRangeParam;
        if (!oneBarRejected && code != TALib.Core.RetCode.Success) throw new InvalidOperationException("TA-Lib returned " + code);
        var (start, count) = range.GetOffsetAndLength(data.Count);
        if (count != data.Count - first || count > 0 && start != first)
            throw new InvalidOperationException("Unexpected extrema alignment.");
        return new(Outputs(name).ToDictionary(key => key, key =>
        {
            var values = Enumerable.Repeat(double.NaN, data.Count).ToArray();
            Array.Copy(key == "Min" ? minimum : maximum, 0, values, start, count);
            return new ComparisonOutput(first, values);
        }, StringComparer.Ordinal));
    }

    private static ComparisonSeries Reference(string name, CompetitorData data, int period) => new(
        Outputs(name).ToDictionary(key => key, key =>
        {
            var values = Enumerable.Repeat(double.NaN, data.Count).ToArray();
            for (var i = period - 1; i < data.Count; i++)
            {
                var sorted = data.Closes.AsSpan(i - period + 1, period).ToArray();
                Array.Sort(sorted);
                values[i] = key == "Min" ? sorted[0] : sorted[^1];
            }
            return new ComparisonOutput(Math.Min(period - 1, data.Count), values);
        }, StringComparer.Ordinal));
}

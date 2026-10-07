using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class WindowExtremeComparison
{
    internal static readonly string[] Names =
    [
        "MinIndex",
        "MaxIndex",
        "MinMaxIndex",
        "MidPoint",
        "MidPrice",
    ];
    internal static readonly ComparisonPair[] Pairs = Names
        .Select(name => new ComparisonPair(
            "TaLib.Functions." + name,
            name.StartsWith("Mid", StringComparison.Ordinal)
                ? "WindowRangeMidpoint"
                : "WindowExtremeIndex",
            (data, period) => Competitor(name, data, period),
            (data, period) => Ooples(name, data, period),
            (data, period) => Reference(name, data, period),
            Outputs(name)
        ))
        .ToArray();

    private static string[] Outputs(string name) =>
        name == "MinMaxIndex" ? ["MinIndex", "MaxIndex"] : [name];

    private static ComparisonSeries Ooples(string name, CompetitorData data, int period)
    {
        var indicators = Outputs(name)
            .ToDictionary(
                key => key,
                key =>
                    key.StartsWith("Mid", StringComparison.Ordinal)
                        ? (IIndicator)new WindowRangeMidpoint(period, key == "MidPrice")
                        : new WindowExtremeIndex(period, key == "MaxIndex")
            );
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicators.Values.ToArray())
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return new(
            indicators.ToDictionary(
                kv => kv.Key,
                kv => new ComparisonOutput(
                    Math.Min(period - 1, data.Count),
                    run[kv.Value.Outputs[0]].ToArray()
                )
            )
        );
    }

    private static ComparisonSeries Competitor(string name, CompetitorData data, int period)
    {
        var a = new double[data.Count];
        var b = new double[data.Count];
        var indices = new int[data.Count];
        System.Range range;
        var code = name switch
        {
            "MinIndex" => Functions.MinIndex<double>(
                data.Closes,
                System.Range.All,
                indices,
                out range,
                period
            ),
            "MaxIndex" => Functions.MaxIndex<double>(
                data.Closes,
                System.Range.All,
                indices,
                out range,
                period
            ),
            "MinMaxIndex" => Functions.MinMaxIndex<double>(
                data.Closes,
                System.Range.All,
                a,
                b,
                out range,
                period
            ),
            "MidPoint" => Functions.MidPoint<double>(
                data.Closes,
                System.Range.All,
                a,
                out range,
                period
            ),
            _ => Functions.MidPrice<double>(
                data.Highs,
                data.Lows,
                System.Range.All,
                a,
                out range,
                period
            ),
        };
        var first = Math.Min(period - 1, data.Count);
        if (
            !(data.Count == 1 && code == TALib.Core.RetCode.OutOfRangeParam)
            && code != TALib.Core.RetCode.Success
        )
            throw new InvalidOperationException("TA-Lib returned " + code);
        var (start, count) = range.GetOffsetAndLength(data.Count);
        if (count != data.Count - first || count > 0 && start != first)
            throw new InvalidOperationException("Unexpected window alignment.");
        return new(
            Outputs(name)
                .ToDictionary(
                    key => key,
                    key =>
                    {
                        var values = Enumerable.Repeat(double.NaN, data.Count).ToArray();
                        for (var i = 0; i < count; i++)
                            values[start + i] =
                                name is "MinIndex" or "MaxIndex" ? indices[i]
                                : key == "MaxIndex" ? b[i]
                                : a[i];
                        return new ComparisonOutput(first, values);
                    }
                )
        );
    }

    private static ComparisonSeries Reference(string name, CompetitorData data, int period) =>
        new(
            Outputs(name)
                .ToDictionary(
                    key => key,
                    key =>
                    {
                        var values = Enumerable.Repeat(double.NaN, data.Count).ToArray();
                        for (var i = period - 1; i < data.Count; i++)
                        {
                            var indices = Enumerable.Range(i - period + 1, period).ToArray();
                            if (key == "MinIndex")
                                values[i] = indices
                                    .OrderBy(j => data.Closes[j])
                                    .ThenByDescending(j => j)
                                    .First();
                            else if (key == "MaxIndex")
                                values[i] = indices
                                    .OrderByDescending(j => data.Closes[j])
                                    .ThenByDescending(j => j)
                                    .First();
                            else
                            {
                                var min = indices.Min(j =>
                                    key == "MidPrice" ? data.Lows[j] : data.Closes[j]
                                );
                                var max = indices.Max(j =>
                                    key == "MidPrice" ? data.Highs[j] : data.Closes[j]
                                );
                                values[i] = (double)(((decimal)min + (decimal)max) / 2);
                            }
                        }
                        return new ComparisonOutput(Math.Min(period - 1, data.Count), values);
                    }
                )
        );

    internal static CompetitorData Fixture() =>
        CompetitorData.FromCloses([3, 1, 1, 4, 4, 2, 4, 1, 1, 1, 3, 3, 2, 2, -1, -1, -3, -3]);
}

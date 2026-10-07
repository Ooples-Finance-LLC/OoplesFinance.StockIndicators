using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class SkenderRocComparison
{
    // Time all three outputs, including an active average with the same period as the lag.
    internal static readonly ComparisonPair Pair = Create(20);

    internal static ComparisonPair Create(int? averagePeriod) =>
        new(
            "Skender.GetRoc",
            "RateOfChangeWithAverage",
            (data, period) => Competitor(data, period, averagePeriod),
            (data, period) => Ooples(data, period, averagePeriod),
            (data, period) => Reference(data, period, averagePeriod),
            ["Momentum", "Roc", "RocSma"]
        );

    private static ComparisonSeries Ooples(CompetitorData data, int period, int? averagePeriod)
    {
        var indicator = new RateOfChangeWithAverage(period, averagePeriod);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return new(
            new Dictionary<string, ComparisonOutput>(StringComparer.Ordinal)
            {
                ["Momentum"] = new(Math.Min(period, data.Count), run[indicator.Momentum].ToArray()),
                ["Roc"] = Masked(
                    period,
                    run[indicator.Roc].ToArray(),
                    run[indicator.RocIsDefined].ToArray()
                ),
                ["RocSma"] = Masked(
                    AverageFirst(period, averagePeriod, data.Count),
                    run[indicator.Average].ToArray(),
                    run[indicator.AverageIsDefined].ToArray()
                ),
            }
        );
    }

    private static ComparisonOutput Masked(int first, double[] values, double[] defined)
    {
        var present = new bool[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            present[i] = defined[i] > 0;
            if (!present[i])
                values[i] = double.NaN;
        }
        return new(Math.Min(first, values.Length), values, present);
    }

    private static int AverageFirst(int period, int? averagePeriod, int count) =>
        averagePeriod.HasValue ? (int)Math.Min(count, (long)period + averagePeriod.Value - 1) : 0;

    private static ComparisonSeries Competitor(CompetitorData data, int period, int? averagePeriod)
    {
        var results = data.Quotes.GetRoc(period, averagePeriod).ToArray();
        ComparisonOutput Output(int first, Func<RocResult, double?> select) =>
            new(
                first,
                results.Select(r => select(r) ?? double.NaN).ToArray(),
                results.Select(r => select(r).HasValue).ToArray()
            );
        return new(
            new Dictionary<string, ComparisonOutput>(StringComparer.Ordinal)
            {
                ["Momentum"] = Output(Math.Min(period, data.Count), r => r.Momentum),
                ["Roc"] = Output(Math.Min(period, data.Count), r => r.Roc),
                ["RocSma"] = Output(AverageFirst(period, averagePeriod, data.Count), r => r.RocSma),
            }
        );
    }

    private static ComparisonSeries Reference(CompetitorData data, int period, int? averagePeriod)
    {
        var momentum = Enumerable.Repeat(double.NaN, data.Count).ToArray();
        var roc = Enumerable.Repeat(double.NaN, data.Count).ToArray();
        var average = Enumerable.Repeat(double.NaN, data.Count).ToArray();
        var rocPresent = new bool[data.Count];
        var averagePresent = new bool[data.Count];
        for (var i = period; i < data.Count; i++)
        {
            var previous = (decimal)data.Closes[i - period];
            var difference = (decimal)data.Closes[i] - previous;
            momentum[i] = (double)difference;
            if (previous == 0)
                continue;
            rocPresent[i] = true;
            roc[i] = (double)(100 * difference / previous);
        }
        var firstAverage = AverageFirst(period, averagePeriod, data.Count);
        if (averagePeriod is int window)
            for (var i = firstAverage; i < data.Count; i++)
            {
                var indices = Enumerable.Range(i - window + 1, window).ToArray();
                if (indices.Any(j => !rocPresent[j]))
                    continue;
                averagePresent[i] = true;
                average[i] = (double)indices.Average(j => (decimal)roc[j]);
            }
        return new(
            new Dictionary<string, ComparisonOutput>(StringComparer.Ordinal)
            {
                ["Momentum"] = new(Math.Min(period, data.Count), momentum),
                ["Roc"] = new(Math.Min(period, data.Count), roc, rocPresent),
                ["RocSma"] = new(firstAverage, average, averagePresent),
            }
        );
    }
}

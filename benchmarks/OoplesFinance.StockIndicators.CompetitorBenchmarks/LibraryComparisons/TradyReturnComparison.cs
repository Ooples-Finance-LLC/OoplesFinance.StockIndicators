using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Trady.Analysis;
using Trady.Core.Infrastructure;
using T = Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class TradyReturnComparison
{
    internal static readonly ComparisonPair[] Pairs = new[]
    {
        "RateOfChange",
        "PercentageDifference",
    }
        .Select(name => new ComparisonPair(
            "Trady.Indicator." + name,
            "RateOfChangeWithValidity",
            (data, period) => Competitor(name, data, period),
            Ooples,
            Reference
        ))
        .ToArray();

    private static ComparisonSeries Ooples(CompetitorData data, int period)
    {
        var indicator = new RateOfChangeWithValidity(period);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var values = run[indicator.Value].ToArray();
        var present = run[indicator.IsDefined].ToArray().Select(value => value > 0).ToArray();
        for (var i = 0; i < values.Length; i++)
            if (!present[i])
                values[i] = double.NaN;
        return Series(period, values, present);
    }

    private static ComparisonSeries Competitor(string name, CompetitorData data, int period)
    {
        var rows =
            name == "RateOfChange"
                ? new T.RateOfChange(data.Candles, period).Compute()
                : new T.PercentageDifference<IOhlcv, AnalyzableTick<decimal?>>(
                    data.Candles,
                    c => c.Close,
                    period
                ).Compute();
        return Series(
            period,
            rows.Select(r => (double?)r.Tick ?? double.NaN).ToArray(),
            rows.Select(r => r.Tick.HasValue).ToArray()
        );
    }

    private static ComparisonSeries Reference(CompetitorData data, int period)
    {
        var values = Enumerable.Repeat(double.NaN, data.Count).ToArray();
        var present = new bool[data.Count];
        for (var i = period; i < data.Count; i++)
        {
            var previous = (decimal)data.Closes[i - period];
            if (previous == 0)
                continue;
            present[i] = true;
            values[i] = (double)(((decimal)data.Closes[i] / previous - 1) * 100);
        }
        return Series(period, values, present);
    }

    internal static ComparisonSeries Series(int period, double[] values, bool[] present) =>
        new(
            new Dictionary<string, ComparisonOutput>(StringComparer.Ordinal)
            {
                ["Value"] = new(Math.Min(period, values.Length), values, present),
            }
        );
}

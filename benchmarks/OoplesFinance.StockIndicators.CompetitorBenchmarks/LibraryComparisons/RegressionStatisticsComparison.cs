using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class RegressionStatisticsComparison
{
    internal static readonly string[] Names = ["Slope", "Intercept", "StdDev", "RSquared", "Line"];
    internal static readonly ComparisonPair Pair = new(
        "QuanTAlib.Slope",
        "WindowRegressionStatistics",
        Quan,
        Ooples,
        (d, p) => Reference(d, p, false),
        Names,
        CompetitorReference: (d, p) => Reference(d, p, true)
    );
    internal static readonly ComparisonPair SnapshotPair = new(
        "Skender.GetSlope",
        "RegressionSnapshot",
        Skender,
        Snapshot,
        (d, p) => Reference(d, p, false, true),
        Names
    );

    private static ComparisonSeries Series(double?[][] values) =>
        new(
            Names
                .Select(
                    (name, slot) =>
                        new KeyValuePair<string, ComparisonOutput>(
                            name,
                            new(
                                0,
                                values[slot].Select(v => v ?? double.NaN).ToArray(),
                                values[slot].Select(v => v.HasValue).ToArray()
                            )
                        )
                )
                .ToDictionary(kv => kv.Key, kv => kv.Value)
        );

    private static ComparisonSeries Quan(CompetitorData data, int period)
    {
        var indicator = new QuanTAlib.Slope(period);
        var values = Enumerable.Range(0, 5).Select(_ => new double?[data.Count]).ToArray();
        for (var i = 0; i < data.Count; i++)
        {
            values[0][i] = indicator
                .Calc(new QuanTAlib.TValue(data.Quotes[i].Date, data.Closes[i], true))
                .Value;
            values[1][i] = indicator.Intercept;
            values[2][i] = indicator.StdDev;
            values[3][i] = indicator.RSquared;
            values[4][i] = indicator.Line;
        }
        return Series(values);
    }

    private static ComparisonSeries Ooples(CompetitorData data, int period)
    {
        var indicator = new WindowRegressionStatistics(period);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var values = Enumerable
            .Range(0, 5)
            .Select(slot =>
            {
                var output = run[indicator.Outputs[slot]].ToArray();
                var present = run[indicator.Outputs[slot + 5]].ToArray();
                return output.Select((v, i) => present[i] > 0 ? (double?)v : null).ToArray();
            })
            .ToArray();
        return Series(values);
    }

    private static ComparisonSeries Skender(CompetitorData data, int period)
    {
        var rows = data.Quotes.GetSlope(period).ToArray();
        return Series([
            rows.Select(r => r.Slope).ToArray(),
            rows.Select(r => r.Intercept).ToArray(),
            rows.Select(r => r.StdDev).ToArray(),
            rows.Select(r => r.RSquared).ToArray(),
            rows.Select(r => (double?)r.Line).ToArray(),
        ]);
    }

    private static ComparisonSeries Snapshot(CompetitorData data, int period)
    {
        var rows = RegressionSnapshot.Calculate(data.IndicatorBars, period);
        return Series([
            rows.Select(r => r.Slope).ToArray(),
            rows.Select(r => r.Intercept).ToArray(),
            rows.Select(r => r.StandardDeviation).ToArray(),
            rows.Select(r => r.RSquared).ToArray(),
            rows.Select(r => r.Line).ToArray(),
        ]);
    }

    // Independent centered batch regression. Decimal moments keep fixture subtraction,
    // covariance and flatness independent of production's rolling binary integer moments.
    private static ComparisonSeries Reference(
        CompetitorData data,
        int period,
        bool native,
        bool snapshot = false
    )
    {
        var values = Enumerable.Range(0, 5).Select(_ => new double?[data.Count]).ToArray();
        double? retained = null;
        decimal finalSlope = 0,
            finalIntercept = 0;
        for (var i = 0; i < data.Count; i++)
        {
            if (snapshot && i + 1 < period)
                continue;
            values[0][i] = 0;
            var count = Math.Min(period, i + 1);
            if (count < 2)
                continue;
            var window = data
                .Closes.Skip(i - count + 1)
                .Take(count)
                .Select(v => (decimal)v)
                .ToArray();
            var mean = window.Sum() / count;
            var center = (count + 1m) / 2;
            decimal xx = 0,
                yy = 0,
                xy = 0;
            for (var j = 0; j < count; j++)
            {
                var dx = j + 1 - center;
                var dy = window[j] - mean;
                xx += dx * dx;
                yy += dy * dy;
                xy += dx * dy;
            }
            var slope = xy / xx;
            var intercept = mean - slope * (snapshot ? i + 1 - (count - 1m) / 2 : center);
            values[0][i] = (double)slope;
            values[1][i] = (double)intercept;
            values[2][i] = Math.Sqrt((double)(yy / count));
            if (!snapshot)
                values[4][i] = (double)(mean + slope * (count - center));
            if (yy != 0)
                retained = (double)(xy * xy / (xx * yy));
            else if (!native)
                retained = null;
            values[3][i] = retained;
            finalSlope = slope;
            finalIntercept = intercept;
        }
        if (snapshot && data.Count >= period)
            for (var i = data.Count - period; i < data.Count; i++)
                values[4][i] = (double)(finalSlope * (i + 1) + finalIntercept);
        return Series(values);
    }

    internal static CompetitorData Fixture() =>
        CompetitorData.FromCloses([1, 2, 4, 9, -2, 8, 0, -3, 2, 2, 2, 2, 17, 17, 17]);
}

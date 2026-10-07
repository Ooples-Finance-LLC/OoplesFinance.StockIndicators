using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TC = Trady.Analysis.Candlestick;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class DelayedDarkCloudComparison
{
    internal static readonly ComparisonPair Pair = Create();

    internal static ComparisonPair Create(
        int delay = 3,
        int longPeriod = 20,
        decimal longThreshold = .75m
    ) =>
        new(
            "Trady.Candlestick.DarkCloudCover",
            "DelayedDarkCloudCoverPattern",
            (data, period) => Competitor(data, period, delay, longPeriod, longThreshold),
            (data, period) => Ooples(data, period, delay),
            (data, period) => Reference(data, period, delay)
        );

    private static ComparisonSeries Competitor(
        CompetitorData data,
        int period,
        int delay,
        int longPeriod,
        decimal longThreshold
    )
    {
        var values = new TC.DarkCloudCover(data.Candles, period, delay, longPeriod, longThreshold)
            .Compute()
            .Select(r =>
                r.Tick.HasValue
                    ? r.Tick.Value
                        ? 1d
                        : 0
                    : double.NaN
            )
            .ToArray();
        return new(Math.Min(delay, data.Count), values);
    }

    private static ComparisonSeries Ooples(CompetitorData data, int period, int delay)
    {
        var indicator = new DelayedDarkCloudCoverPattern(period, delay);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return new(Math.Min(delay, data.Count), run[indicator.Outputs[0]].ToArray());
    }

    private static ComparisonSeries Reference(CompetitorData data, int period, int delay)
    {
        var values = new double[data.Count];
        for (var i = delay; i < data.Count; i++)
        {
            var a = i - delay;
            var b = a + 1;
            if (a < period)
                continue;
            var trend = Enumerable
                .Range(a - period + 1, period)
                .All(j => data.Highs[j] > data.Highs[j - 1] && data.Lows[j] > data.Lows[j - 1]);
            var midpoint = ((decimal)data.Opens[a] + (decimal)data.Closes[a]) / 2;
            values[i] =
                trend
                && data.Closes[a] > data.Opens[a]
                && data.Closes[b] < data.Opens[b]
                && data.Opens[b] > data.Closes[a]
                && (decimal)data.Closes[b] < midpoint
                    ? 1
                    : 0;
        }
        return new(Math.Min(delay, data.Count), values);
    }

    internal static readonly (double O, double H, double L, double C)[] Candidates =
    [
        (17, 18, 8, 10),
        (16, 18, 8, 10),
        (17, 18, 8, 13),
        (17, 18, 8, 12.875),
        (17, 18, 8, 8),
        (8, 18, 8, 17),
    ];

    internal static CompetitorData Fixture(int period = 3, int delay = 3)
    {
        var bars = new List<(double O, double H, double L, double C)>();
        foreach (var candidate in Candidates)
        {
            for (var i = 0; i <= period; i++)
            {
                var offset = 2d * (period - i);
                bars.Add((10 - offset, 20 - offset, 0 - offset, 16 - offset));
            }
            bars.Add(candidate);
            // Rising later candles deliberately violate downtrend confirmation.
            for (var i = 1; i < delay; i++)
                bars.Add((30 + 2 * i, 40 + 2 * i, 20 + 2 * i, 36 + 2 * i));
        }
        return CompetitorData.FromOhlc(
            bars.Select(b => b.O).ToArray(),
            bars.Select(b => b.H).ToArray(),
            bars.Select(b => b.L).ToArray(),
            bars.Select(b => b.C).ToArray()
        );
    }
}

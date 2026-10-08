using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;
using T = Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class VolumeRecurrenceComparison
{
    internal static readonly string[] Ids =
    [
        "Skender.GetForceIndex",
        "Trady.Indicator.PositiveVolumeIndex",
        "Trady.Indicator.NegativeVolumeIndex",
    ];
    internal static readonly ComparisonPair[] Pairs = Ids.Select(id => new ComparisonPair(
            id,
            id == Ids[0] ? "SeededForceIndex" : "VolumeConditionedIndex",
            (d, p) => Native(id, d, p),
            (d, p) => Owned(id, d, p),
            (d, p) => Reference(id, d, p, false),
            CompetitorReference: (d, p) => Reference(id, d, p, true)
        ))
        .ToArray();

    private static ComparisonSeries Owned(string id, CompetitorData data, int period)
    {
        IIndicator indicator =
            id == Ids[0] ? new SeededForceIndex(period) : new VolumeConditionedIndex(id == Ids[1]);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var values = run[indicator.Outputs[0]].ToArray();
        var flags = run[indicator.Outputs[1]].ToArray();
        return VolumePriceComparison.Mask(
            values.Select((v, i) => flags[i] > 0 ? (double?)v : null).ToArray()
        );
    }

    private static ComparisonSeries Native(string id, CompetitorData data, int period) =>
        VolumePriceComparison.Mask(
            id == Ids[0] ? data.Quotes.GetForceIndex(period).Select(r => r.ForceIndex).ToArray()
            : id == Ids[1]
                ? new T.PositiveVolumeIndex(data.Candles)
                    .Compute()
                    .Select(r => (double?)r.Tick)
                    .ToArray()
            : new T.NegativeVolumeIndex(data.Candles)
                .Compute()
                .Select(r => (double?)r.Tick)
                .ToArray()
        );

    private static ComparisonSeries Reference(
        string id,
        CompetitorData data,
        int period,
        bool native
    )
    {
        if (id == Ids[0])
            return ForceReference(data, period, native);
        var values = new double?[data.Count];
        var index = 100d;
        var decimalIndex = 100m;
        var positive = id == Ids[1];
        for (var i = 0; i < data.Count; i++)
        {
            if (native)
            {
                if (
                    i > 0
                    && (
                        positive
                            ? (decimal)data.Volumes[i] > (decimal)data.Volumes[i - 1]
                            : (decimal)data.Volumes[i] < (decimal)data.Volumes[i - 1]
                    )
                )
                {
                    var prior = (decimal)data.Closes[i - 1];
                    var current = (decimal)data.Closes[i];
                    decimalIndex *= 1 + (current - prior) / prior;
                }
                values[i] = (double)decimalIndex;
            }
            else
            {
                if (
                    i > 0
                    && (
                        positive
                            ? data.Volumes[i] > data.Volumes[i - 1]
                            : data.Volumes[i] < data.Volumes[i - 1]
                    )
                )
                {
                    var previous = Units(data.Closes[i - 1]);
                    if (previous.IsZero)
                        break;
                    index = Round(Units(index) * Units(data.Closes[i]), previous * Grid);
                }
                values[i] = index;
            }
        }
        return VolumePriceComparison.Mask(values);
    }

    private static ComparisonSeries ForceReference(CompetitorData data, int period, bool native)
    {
        var values = new double?[data.Count];
        var seed = BigInteger.Zero;
        double nativeSeed = 0,
            previous = 0;
        var alpha = Divide(2, period + 1d);
        for (var i = 1; i < data.Count; i++)
        {
            if (native)
            {
                var raw = Multiply(
                    Price(data.Volumes[i]),
                    Subtract(Price(data.Closes[i]), Price(data.Closes[i - 1]))
                );
                if (i <= period)
                    nativeSeed = Add(nativeSeed, raw);
                if (i < period)
                    continue;
                previous =
                    i == period
                        ? Divide(nativeSeed, period)
                        : Add(previous, Multiply(alpha, Subtract(raw, previous)));
            }
            else
            {
                var raw =
                    (Units(data.Closes[i]) - Units(data.Closes[i - 1])) * Units(data.Volumes[i]);
                if (i <= period)
                    seed += raw;
                if (i < period)
                    continue;
                previous =
                    i == period
                        ? Round(seed, Grid * Grid * period)
                        : Round(
                            Units(previous) * Grid * (period - 1) + 2 * raw,
                            Grid * Grid * (period + 1L)
                        );
            }
            values[i] = previous;
        }
        return VolumePriceComparison.Mask(values);
    }

    private static double Price(double value) => (double)(decimal)value;

    internal static CompetitorData Fixture()
    {
        double[] prices = [-3, -6, 3, 6, 2, 4, 1, 2, -1, -2, 4, 3];
        return CompetitorData.FromOhlcv(
            prices,
            prices,
            prices,
            prices,
            [2, 4, 4, 1, 3, 2, 5, 1, 4, 2, 6, 3]
        );
    }

    internal static CompetitorData CollapsedVolumeFixture() =>
        CompetitorData.FromOhlcv(
            [1, 2, 3, 4],
            [1, 2, 3, 4],
            [1, 2, 3, 4],
            [1, 2, 3, 4],
            [.1, Math.BitIncrement(.1), .1, Math.BitIncrement(.1)]
        );

    internal static CompetitorData ForceCancellationFixture() =>
        CompetitorData.FromOhlcv([0, 1, 2], [0, 1, 2], [0, 1, 2], [0, 1, 2], [1, 1e20, 1]);
}

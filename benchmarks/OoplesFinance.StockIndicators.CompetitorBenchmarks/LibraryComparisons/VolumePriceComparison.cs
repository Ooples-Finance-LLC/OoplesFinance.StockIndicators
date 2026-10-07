using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;
using T = Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class VolumePriceComparison
{
    internal static readonly ComparisonPair[] Pairs =
    [
        Vwap(),
        Trady(),
        new(
            "Skender.GetVwma",
            "VolumeWeightedPrice",
            (d, p) => Mask(d.Quotes.GetVwma(p).Select(r => r.Vwma).ToArray()),
            (d, p) => Owned(d, p, false, null),
            (d, p) => Exact(d, p, false, null),
            CompetitorReference: (d, p) => Binary(d, p, false, null)
        ),
    ];

    internal static ComparisonPair Vwap(DateTime? anchor = null) =>
        new(
            "Skender.GetVwap",
            "VolumeWeightedPrice",
            (d, _) => Mask(d.Quotes.GetVwap(anchor).Select(r => r.Vwap).ToArray()),
            (d, _) => Owned(d, null, true, anchor),
            (d, _) => Exact(d, null, true, anchor),
            CompetitorReference: (d, _) => Binary(d, null, true, anchor)
        );

    internal static ComparisonPair Trady(int? period = null) =>
        new(
            "Trady.Indicator.VolumeWeightedAveragePrice",
            "VolumeWeightedPrice",
            (d, _) =>
                Mask(
                    new T.VolumeWeightedAveragePrice(d.Candles, period)
                        .Compute()
                        .Select(r => (double?)r.Tick)
                        .ToArray()
                ),
            (d, _) => Owned(d, period, true, null),
            (d, _) => Exact(d, period, true, null),
            CompetitorReference: (d, _) => Decimal(d, period)
        );

    internal static ComparisonSeries Mask(double?[] values) =>
        new(new Dictionary<string, ComparisonOutput> { ["Value"] = Nullable(values) });

    internal static ComparisonOutput Nullable(double?[] values) =>
        new(
            0,
            values.Select(v => v ?? double.NaN).ToArray(),
            values.Select(v => v.HasValue).ToArray()
        );

    private static ComparisonSeries Owned(
        CompetitorData data,
        int? period,
        bool typical,
        DateTime? anchor
    )
    {
        var indicator = new VolumeWeightedPrice(period, typical, anchor);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var values = run[indicator.Value].ToArray();
        var flags = run[indicator.IsDefined].ToArray();
        return Mask(values.Select((v, i) => flags[i] > 0 ? (double?)v : null).ToArray());
    }

    private static ComparisonSeries Exact(
        CompetitorData data,
        int? period,
        bool typical,
        DateTime? anchor
    )
    {
        var values = new double?[data.Count];
        // Independent batch prefix sums; production evicts complete bars from a queue.
        var prices = new List<BigInteger> { BigInteger.Zero };
        var volumes = new List<BigInteger> { BigInteger.Zero };
        for (var i = 0; i < data.Count; i++)
        {
            if (anchor.HasValue && data.Dates[i] < anchor.Value)
                continue;
            var v = Units(data.Volumes[i]);
            var price = Units(data.Closes[i]);
            if (typical)
                price += Units(data.Highs[i]) + Units(data.Lows[i]);
            prices.Add(prices[^1] + price * v);
            volumes.Add(volumes[^1] + v);
            var count = prices.Count - 1;
            if (period is int length && count < length)
                continue;
            var first = period is int p ? count - p : 0;
            var denominator = volumes[count] - volumes[first];
            if (denominator.IsZero)
                continue;
            values[i] = Round(
                prices[count] - prices[first],
                denominator * Grid * (typical ? 3 : 1)
            );
        }
        return Mask(values);
    }

    private static ComparisonSeries Binary(
        CompetitorData data,
        int? period,
        bool typical,
        DateTime? anchor
    )
    {
        var values = new double?[data.Count];
        double total = 0,
            mass = 0;
        for (var i = 0; i < data.Count; i++)
        {
            if (anchor.HasValue && data.Dates[i] < anchor.Value)
                continue;
            if (period.HasValue)
            {
                if (i < period.Value - 1)
                    continue;
                total = mass = 0;
                for (var j = i - period.Value + 1; j <= i; j++)
                {
                    total = Add(total, Multiply(Price(data.Closes[j]), Price(data.Volumes[j])));
                    mass = Add(mass, Price(data.Volumes[j]));
                }
            }
            else
            {
                var volume = Price(data.Volumes[i]);
                var sum = typical
                    ? Add(Add(Price(data.Highs[i]), Price(data.Lows[i])), Price(data.Closes[i]))
                    : Price(data.Closes[i]);
                var product = Multiply(volume, sum);
                if (typical)
                    product = Divide(product, 3);
                total = Add(total, product);
                mass = Add(mass, volume);
            }
            if (Math.Abs(mass) > 0)
                values[i] = Divide(total, mass);
        }
        return Mask(values);
    }

    private static double Price(double value) => (double)(decimal)value;

    private static ComparisonSeries Decimal(CompetitorData data, int? period)
    {
        var values = new double?[data.Count];
        for (var i = period.HasValue ? period.Value - 1 : 0; i < data.Count; i++)
        {
            decimal total = 0,
                mass = 0;
            for (var j = period.HasValue ? i - period.Value + 1 : 0; j <= i; j++)
            {
                var typical =
                    ((decimal)data.Highs[j] + (decimal)data.Lows[j] + (decimal)data.Closes[j]) / 3;
                total += typical * (decimal)data.Volumes[j];
                mass += (decimal)data.Volumes[j];
            }
            values[i] = (double)(total / mass);
        }
        return Mask(values);
    }

    internal static CompetitorData Fixture(bool zeros = false) =>
        CompetitorData.FromOhlcv(
            [1, 3, 2, -3, -4, 0, 1, 5, 3, 2, 7, 1],
            [3, 5, 4, -1, -2, 0, 2, 7, 4, 3, 9, 2],
            [0, 1, 1, -5, -6, 0, 0, 3, 1, 1, 5, 0],
            [2, 4, 2, -3, -5, 0, 1, 6, 2, 2, 8, 1],
            zeros ? [0, 0, 2, 0, 0, 3, 1, 0, 2, 4, 0, 3] : [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]
        );

    internal static CompetitorData CollapsedFixture()
    {
        double[] close = [.1, Math.BitIncrement(.1), .1, Math.BitIncrement(.1)];
        return CompetitorData.FromOhlcv(close, close, close, close, [1, 2, 3, 4]);
    }
}

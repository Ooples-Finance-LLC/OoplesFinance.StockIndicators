using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using TALib;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;
using T = Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class CommodityChannelComparison
{
    internal static readonly string[] Ids =
    [
        "Skender.GetCci",
        "TaLib.Functions.Cci",
        "Trady.Indicator.CommodityChannelIndex",
    ];
    internal static readonly ComparisonPair[] Pairs = Enumerable
        .Range(0, 3)
        .Select(Create)
        .ToArray();

    internal static ComparisonPair Create(int variant) =>
        new(
            Ids[variant],
            nameof(WindowCommodityChannelIndex),
            (d, p) => Native(d, p, variant),
            (d, p) => Owned(d.IndicatorBars, p, variant),
            (d, p) => VolumePriceComparison.Mask(Reference(d.IndicatorBars, p, variant)),
            MinimumInputCount: variant == 1 ? 2 : 1,
            CompetitorReference: (d, p) =>
                VolumePriceComparison.Mask(
                    variant == 2
                        ? TradyReference(
                                d.Candles.Select(c => (c.High, c.Low, c.Close)).ToArray(),
                                p
                            )
                            .Select(v => (double?)v)
                            .ToArray()
                        : FloatingReference(
                            variant == 0
                                ? d
                                    .Quotes.Select(q =>
                                        ((double)q.High, (double)q.Low, (double)q.Close)
                                    )
                                    .ToArray()
                                : d.IndicatorBars.Select(b => (b.High, b.Low, b.Close)).ToArray(),
                            p,
                            variant == 1
                        )
                ),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    private static ComparisonSeries Native(CompetitorData data, int period, int variant)
    {
        if (variant == 0)
            return VolumePriceComparison.Mask(
                data.Quotes.GetCci(period).Select(r => r.Cci).ToArray()
            );
        if (variant == 2)
            return VolumePriceComparison.Mask(
                new T.CommodityChannelIndex(data.Candles, period)
                    .Compute()
                    .Select(r => (double?)r.Tick)
                    .ToArray()
            );
        var result = new double?[data.Count];
        if (data.Count == 0)
            return VolumePriceComparison.Mask(result);
        var packed = new double[data.Count];
        var code = Functions.Cci<double>(
            data.Highs,
            data.Lows,
            data.Closes,
            System.Range.All,
            packed,
            out var range,
            period
        );
        if (code != TALib.Core.RetCode.Success)
            throw new InvalidOperationException("TA-Lib CCI returned " + code);
        var (start, count) = range.GetOffsetAndLength(data.Count);
        if (count != Math.Max(0, data.Count - period + 1) || count > 0 && start != period - 1)
            throw new InvalidOperationException("Unexpected CCI alignment");
        for (var i = 0; i < count; i++)
            result[start + i] = packed[i];
        return VolumePriceComparison.Mask(result);
    }

    internal static ComparisonSeries Owned(Bar[] bars, int period, int variant)
    {
        var indicator = new WindowCommodityChannelIndex(period, variant == 2, variant != 0);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var values = run[indicator.Value].ToArray();
        var present = run[indicator.IsDefined].ToArray();
        return VolumePriceComparison.Mask(
            values.Select((v, i) => present[i] > 0 ? (double?)v : null).ToArray()
        );
    }

    internal static double?[] Reference(Bar[] bars, int period, int variant)
    {
        var prices = bars.Select(b =>
                Units(Round(Units(b.High) + Units(b.Low) + Units(b.Close), 3 * Grid))
            )
            .ToArray();
        var means = new BigInteger[prices.Length];
        var result = new double?[prices.Length];
        for (var i = period - 1; i < prices.Length; i++)
        {
            means[i] = Units(
                Round(
                    prices
                        .Skip(i - period + 1)
                        .Take(period)
                        .Aggregate(BigInteger.Zero, (s, v) => s + v),
                    Grid * period
                )
            );
            if (variant == 2 && (long)i < 2L * (period - 1))
                continue;
            var deviation = BigInteger.Zero;
            for (var j = i - period + 1; j <= i; j++)
                deviation += BigInteger.Abs(prices[j] - (variant == 2 ? means[j] : means[i]));
            result[i] = deviation.IsZero
                ? variant != 0
                    ? 0
                    : null
                : Round(200 * new BigInteger(period) * (prices[i] - means[i]), 3 * deviation);
        }
        return result;
    }

    internal static double?[] FloatingReference(
        (double High, double Low, double Close)[] bars,
        int period,
        bool ta
    )
    {
        if (ta && bars.Length < period)
            return new double?[bars.Length];
        var prices = bars.Select(b => Divide(Add(Add(b.High, b.Low), b.Close), 3)).ToArray();
        var result = new double?[bars.Length];
        var ring = new double[Math.Min(period, bars.Length)];
        for (var i = 0; i < prices.Length; i++)
        {
            ring[i % period] = prices[i];
            if (i < period - 1)
                continue;
            var window = ta ? ring : prices.Skip(i - period + 1).Take(period).ToArray();
            var mean = Divide(window.Aggregate(0d, Add), period);
            var sum = window.Select(v => Math.Abs(Subtract(v, mean))).Aggregate(0d, Add);
            var average = Divide(sum, period);
            var distance = Subtract(prices[i], mean);
            var denominator = Multiply(.015, average);
            if (ta)
                result[i] = distance != 0 && denominator != 0 ? Divide(distance, denominator) : 0; // NOSONAR: Native CCI branches on exact zero.
            else
                result[i] = average == 0 ? null : Divide(distance, denominator); // NOSONAR: Native CCI branches on exact zero.
        }
        return result;
    }

    internal static decimal?[] TradyReference(
        (decimal High, decimal Low, decimal Close)[] bars,
        int period
    )
    {
        if ((long)bars.Length <= 2L * (period - 1))
            return new decimal?[bars.Length];
        var prices = bars.Select(b => (b.High + b.Low + b.Close) / 3).ToArray();
        var result = new decimal?[bars.Length];
        var means = new decimal[bars.Length];
        for (var i = period - 1; i < bars.Length; i++)
        {
            means[i] = prices.Skip(i - period + 1).Take(period).Average();
            if ((long)i < 2L * (period - 1))
                continue;
            var deviation = Enumerable
                .Range(i - period + 1, period)
                .Select(j => Math.Abs(prices[j] - means[j]))
                .Average();
            // Trady's conditional default literal resolves to decimal zero, not nullable absence.
            result[i] = deviation == 0 ? 0 : (prices[i] - means[i]) / (.015m * deviation);
        }
        return result;
    }

    internal static CompetitorData Fixture() =>
        CompetitorData.FromOhlc(
            [3, 7, 4, 9, 2, 11, -1, 4, 8, 3],
            [7, 11, 8, 15, 6, 15, 3, 8, 12, 7],
            [-1, 3, 0, 5, -2, 7, -5, 0, 4, -1],
            [5, 4, 7, 8, 1, 13, 0, 7, 5, 6]
        );
}

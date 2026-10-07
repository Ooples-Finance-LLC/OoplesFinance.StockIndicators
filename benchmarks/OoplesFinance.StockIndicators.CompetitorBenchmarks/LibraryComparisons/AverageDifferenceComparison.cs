using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Trady.Analysis.Infrastructure;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;
using T = Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class AverageDifferenceComparison
{
    internal static readonly ComparisonPair[] Pairs = [Create(false), Create(true)];

    internal static ComparisonPair Create(bool exponential, int first = 12, int second = 26) =>
        new(
            "Trady.Indicator."
                + (exponential ? "Exponential" : "Simple")
                + "MovingAverageOscillator",
            nameof(MovingAverageDifference),
            (d, _) => Native(d, first, second, exponential),
            (d, _) => Owned(d.IndicatorBars, first, second, exponential),
            (d, _) => VolumePriceComparison.Mask(Reference(d.Closes, first, second, exponential)),
            CompetitorReference: (d, _) =>
                VolumePriceComparison.Mask(
                    NativeReference(
                            d.Candles.Select(c => c.Close).ToArray(),
                            first,
                            second,
                            exponential
                        )
                        .Select(v => (double?)v)
                        .ToArray()
                ),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static AnalyzableBase<decimal, decimal, decimal?, decimal?> Tuple(
        decimal[] values,
        int first,
        int second,
        bool exponential
    ) =>
        exponential
            ? new T.ExponentialMovingAverageOscillatorByTuple(values, first, second)
            : new T.SimpleMovingAverageOscillatorByTuple(values, first, second);

    private static ComparisonSeries Native(
        CompetitorData data,
        int first,
        int second,
        bool exponential
    ) =>
        VolumePriceComparison.Mask(
            (
                exponential
                    ? new T.ExponentialMovingAverageOscillator(data.Candles, first, second)
                        .Compute()
                        .Select(r => (double?)r.Tick)
                    : new T.SimpleMovingAverageOscillator(data.Candles, first, second)
                        .Compute()
                        .Select(r => (double?)r.Tick)
            ).ToArray()
        );

    internal static ComparisonSeries Owned(Bar[] bars, int first, int second, bool exponential)
    {
        var indicator = new MovingAverageDifference(first, second, exponential);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var present = run[indicator.IsDefined].ToArray();
        return VolumePriceComparison.Mask(
            run[indicator.Value]
                .ToArray()
                .Select((v, i) => present[i] > 0 ? (double?)v : null)
                .ToArray()
        );
    }

    internal static double?[] Reference(double[] prices, int first, int second, bool exponential)
    {
        double?[] Mean(int period)
        {
            var result = new double?[prices.Length];
            for (var i = 0; i < prices.Length; i++)
                if (exponential)
                    result[i] =
                        i == 0
                            ? prices[i]
                            : Round(
                                2 * Units(prices[i]) + (period - 1) * Units(result[i - 1]!.Value),
                                ((BigInteger)period + 1) * Grid
                            );
                else if (i >= period - 1)
                    result[i] = Round(
                        prices
                            .Skip(i - period + 1)
                            .Take(period)
                            .Aggregate(BigInteger.Zero, (s, v) => s + Units(v)),
                        period * Grid
                    );
            return result;
        }
        var a = Mean(first);
        var b = Mean(second);
        return a.Select(
                (v, i) =>
                    v.HasValue && b[i].HasValue
                        ? (double?)Round(Units(v.Value) - Units(b[i]!.Value), Grid)
                        : null
            )
            .ToArray();
    }

    internal static decimal?[] NativeReference(
        decimal[] prices,
        int first,
        int second,
        bool exponential
    )
    {
        decimal?[] Mean(int period)
        {
            var result = new decimal?[prices.Length];
            var alpha = exponential ? 2m / (period + 1) : 0;
            for (var i = 0; i < prices.Length; i++)
                if (exponential)
                    result[i] =
                        i == 0 ? prices[i] : result[i - 1] + alpha * (prices[i] - result[i - 1]);
                else if (period > 0 && i >= period - 1)
                {
                    decimal sum = 0;
                    for (var j = i - period + 1; j <= i; j++)
                        sum += prices[j];
                    result[i] = sum / period;
                }
            return result;
        }
        var a = Mean(first);
        var b = Mean(second);
        return a.Select((v, i) => v - b[i]).ToArray();
    }
}

using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using TALib;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;
using TaCore = TALib.Core;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class DelayedHilbertTrendComparison
{
    internal static readonly string[] Names = ["Trendline"];

    internal static ComparisonSeries Series(double?[] values) =>
        RetrospectivePriceComparison.Series(Names, [values]);

    internal static ComparisonPair Pair(int suppression = 0) =>
        new(
            "TaLib.Functions.HtTrendline",
            nameof(DelayedHilbertTrendline),
            (d, _) => Native(d.Closes),
            (d, _) => Owned(d.IndicatorBars, suppression),
            (d, _) => Series(Reference(d.Closes, suppression)),
            Names,
            MinimumInputCount: 2,
            CompetitorReference: (d, _) =>
            {
                var packed = NativePacked(d.Closes, suppression, 0, d.Count - 1);
                var result = new double?[d.Count];
                for (var i = 0; i < packed.Length; i++)
                    result[63 + suppression + i] = packed[i];
                return Series(result);
            },
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal sealed class Settings : IDisposable
    {
        private readonly int _old = TaCore.UnstablePeriodSettings.Get(
            TaCore.UnstableFunc.HtTrendline
        );

        internal Settings(int suppression) =>
            TaCore.UnstablePeriodSettings.Set(TaCore.UnstableFunc.HtTrendline, suppression);

        public void Dispose() =>
            TaCore.UnstablePeriodSettings.Set(TaCore.UnstableFunc.HtTrendline, _old);
    }

    internal static ComparisonSeries Owned(Bar[] bars, int suppression)
    {
        var owner = new DelayedHilbertTrendline(suppression);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(owner)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var values = run[owner.Trendline].ToArray();
        var flags = run[owner.IsDefined].ToArray();
        return Series(values.Select((v, i) => flags[i] > 0 ? (double?)v : null).ToArray());
    }

    private static ComparisonSeries Native(double[] prices)
    {
        var result = new double?[prices.Length];
        if (prices.Length == 0)
            return Series(result);
        var output = new double[prices.Length];
        var code = Functions.HtTrendline<double>(prices, System.Range.All, output, out var range);
        if (code != TaCore.RetCode.Success)
            throw new InvalidOperationException("TA Hilbert trendline: " + code);
        for (var i = range.Start.Value; i < range.End.Value; i++)
            result[i] = output[i - range.Start.Value];
        return Series(result);
    }

    internal static double?[] Reference(double[] prices, int suppression)
    {
        var result = new double?[prices.Length];
        if (63L + suppression >= prices.Length)
            return result;
        var periods = SeededPhaseComparison.GridReference(
            prices,
            .5,
            .05,
            false,
            true,
            0,
            true,
            37,
            37
        )[2];
        var means = new BigInteger[prices.Length];
        for (var i = 37; i < prices.Length; i++)
        {
            var count = (int)(periods[i]!.Value + .5);
            if (count > 0)
            {
                var sum = Enumerable
                    .Range(i - count + 1, count)
                    .Aggregate(BigInteger.Zero, (s, j) => s + Units(prices[j]));
                means[i] = DirectionalComparison.RoundedUnits(sum, count);
            }
            if (i >= 63L + suppression)
                result[i] = Round(
                    4 * means[i] + 3 * means[i - 1] + 2 * means[i - 2] + means[i - 3],
                    10 * Grid
                );
        }
        return result;
    }

    internal static T[] NativePacked<T>(T[] prices, int suppression, int start, int end)
        where T : IFloatingPointIeee754<T>
    {
        var lookback = 63L + suppression;
        if (lookback > end)
            return [];
        start = Math.Max(start, (int)lookback);
        if (start > end)
            return [];
        var begin = start - (int)lookback;
        var periods = DelayedPhaseComparison.NativePacked(
            prices,
            .5,
            .05,
            suppression,
            start,
            end,
            true,
            37,
            63,
            true
        )[2];
        var means = new T[periods.Length + 3];
        var result = new List<T>();
        for (var j = 0; j < periods.Length; j++)
        {
            var today = begin + 37 + j;
            var count = int.CreateTruncating(periods[j] + T.CreateChecked(.5));
            var sum = T.Zero;
            for (var lag = 0; lag < count; lag++)
                sum += prices[today - lag];
            means[j + 3] = count > 0 ? sum / T.CreateChecked(count) : T.Zero;
            var value =
                (
                    T.CreateChecked(4) * means[j + 3]
                    + T.CreateChecked(3) * means[j + 2]
                    + T.CreateChecked(2) * means[j + 1]
                    + means[j]
                ) / T.CreateChecked(10);
            if (today >= start)
                result.Add(value);
        }
        return result.ToArray();
    }
}

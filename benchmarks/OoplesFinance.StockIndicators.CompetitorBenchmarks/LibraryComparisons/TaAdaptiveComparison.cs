using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using TALib;
using TaCore = TALib.Core;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class TaAdaptiveComparison
{
    internal static ComparisonPair Pair(int suppression = 0) =>
        new(
            "TaLib.Functions.Kama",
            nameof(SeededAdaptiveAverage),
            Native,
            (d, p) => Owned(d.IndicatorBars, p, suppression),
            (d, p) =>
                TradyAdaptiveComparison.Series(
                    SeededAdaptiveComparison.ReferenceValues(
                        d.Closes,
                        p,
                        2,
                        30,
                        false,
                        false,
                        false,
                        true,
                        1L + suppression
                    )[0]
                ),
            ["Kama"],
            MinimumInputCount: 2,
            CompetitorReference: (d, p) => Reference(d, p, suppression),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal sealed class Settings : IDisposable
    {
        private readonly int _old = TaCore.UnstablePeriodSettings.Get(TaCore.UnstableFunc.Kama);

        internal Settings(int value) =>
            TaCore.UnstablePeriodSettings.Set(TaCore.UnstableFunc.Kama, value);

        public void Dispose() => TaCore.UnstablePeriodSettings.Set(TaCore.UnstableFunc.Kama, _old);
    }

    internal static ComparisonSeries Owned(Bar[] bars, int p, int suppression)
    {
        var indicator = new SeededAdaptiveAverage(
            p,
            resetFlat: false,
            fastFlat: true,
            outputDelay: 1L + suppression
        );
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var v = run[indicator.Average].ToArray();
        var f = run[indicator.AverageIsDefined].ToArray();
        return TradyAdaptiveComparison.Series(
            v.Select((x, i) => f[i] > 0 ? (double?)x : null).ToArray()
        );
    }

    private static ComparisonSeries Native(CompetitorData d, int p)
    {
        var r = new double?[d.Count];
        if (d.Count > 0)
        {
            var output = new double[d.Count];
            var code = Functions.Kama<double>(d.Closes, System.Range.All, output, out var range, p);
            if (code != TaCore.RetCode.Success)
                throw new InvalidOperationException("TA KAMA failed: " + code);
            for (var i = range.Start.Value; i < range.End.Value; i++)
                r[i] = output[i - range.Start.Value];
        }
        return TradyAdaptiveComparison.Series(r);
    }

    private static ComparisonSeries Reference(CompetitorData d, int p, int suppression)
    {
        var r = new double?[d.Count];
        var values = NativePacked(d.Closes, p, suppression, 0, d.Count - 1);
        for (var i = (long)p + suppression; i < d.Count; i++)
            r[(int)i] = values[(int)(i - p - suppression)];
        return TradyAdaptiveComparison.Series(r);
    }

    // Independent range-local sliding-change reference. Native rolling rounding and its
    // signed-change saturation condition are preserved rather than replaced by a full scan.
    internal static T[] NativePacked<T>(T[] prices, int p, int suppression, int start, int end)
        where T : IFloatingPointIeee754<T>
    {
        var lookback = (long)p + suppression;
        if (lookback > end)
            return [];
        start = Math.Max(start, (int)lookback);
        var begin = start - (int)lookback;
        var volatility = T.Zero;
        for (var i = begin; i < begin + p; i++)
            volatility += T.Abs(prices[i] - prices[i + 1]);
        var average = prices[begin + p - 1];
        var slow = T.CreateChecked(2) / (T.CreateChecked(30) + T.One);
        var difference = T.CreateChecked(2) / (T.CreateChecked(2) + T.One) - slow;
        var result = new List<T>();
        for (var i = begin + p; i <= end; i++)
        {
            if (i > begin + p)
            {
                volatility -= T.Abs(prices[i - p - 1] - prices[i - p]);
                volatility += T.Abs(prices[i] - prices[i - 1]);
            }
            var change = prices[i] - prices[i - p];
            var er =
                volatility <= change || T.IsZero(volatility) ? T.One : T.Abs(change / volatility);
            var rate = er * difference + slow;
            var square = rate * rate;
            average = (prices[i] - average) * square + average;
            if (i >= start)
                result.Add(average);
        }
        return result.ToArray();
    }
}

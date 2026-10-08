using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using TALib;
using TaCore = TALib.Core;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class DelayedPhaseComparison
{
    internal static ComparisonPair Pair(double fast = .5, double slow = .05, int suppression = 0) =>
        new(
            "TaLib.Functions.Mama",
            nameof(DelayedPhaseAdaptiveAverage),
            (d, _) => Native(d, fast, slow),
            (d, _) => Owned(d.IndicatorBars, fast, slow, suppression),
            (d, _) =>
                SeededPhaseComparison.Series(
                    SeededPhaseComparison.GridReference(
                        d.Closes,
                        fast,
                        slow,
                        false,
                        true,
                        suppression
                    )
                ),
            SeededPhaseComparison.Names,
            MinimumInputCount: 2,
            CompetitorReference: (d, _) => Reference(d, fast, slow, suppression),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal sealed class Settings : IDisposable
    {
        private readonly int _old = TaCore.UnstablePeriodSettings.Get(TaCore.UnstableFunc.Mama);

        internal Settings(int suppression) =>
            TaCore.UnstablePeriodSettings.Set(TaCore.UnstableFunc.Mama, suppression);

        public void Dispose() => TaCore.UnstablePeriodSettings.Set(TaCore.UnstableFunc.Mama, _old);
    }

    internal static ComparisonSeries Owned(Bar[] bars, double fast, double slow, int suppression)
    {
        var indicator = new DelayedPhaseAdaptiveAverage(fast, slow, suppression);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var r = new double?[2][];
        for (var j = 0; j < 2; j++)
        {
            var v = run[indicator.Outputs[j]].ToArray();
            var f = run[indicator.Outputs[j + 2]].ToArray();
            r[j] = v.Select((x, i) => f[i] > 0 ? (double?)x : null).ToArray();
        }
        return SeededPhaseComparison.Series(r);
    }

    private static ComparisonSeries Native(CompetitorData d, double fast, double slow)
    {
        var r = Enumerable.Range(0, 2).Select(_ => new double?[d.Count]).ToArray();
        if (d.Count == 0)
            return SeededPhaseComparison.Series(r);
        var output = new[] { new double[d.Count], new double[d.Count] };
        var code = Functions.Mama<double>(
            d.Closes,
            System.Range.All,
            output[0],
            output[1],
            out var range,
            fast,
            slow
        );
        if (code != TaCore.RetCode.Success)
            throw new InvalidOperationException("TA MAMA failed: " + code);
        for (var j = 0; j < 2; j++)
        for (var i = range.Start.Value; i < range.End.Value; i++)
            r[j][i] = output[j][i - range.Start.Value];
        return SeededPhaseComparison.Series(r);
    }

    private static ComparisonSeries Reference(
        CompetitorData d,
        double fast,
        double slow,
        int suppression
    )
    {
        var r = Enumerable.Range(0, 2).Select(_ => new double?[d.Count]).ToArray();
        var values = NativePacked(d.Closes, fast, slow, suppression, 0, d.Count - 1);
        for (var j = 0; j < 2; j++)
        for (var i = 32L + suppression; i < d.Count; i++)
            r[j][(int)i] = values[j][(int)(i - 32 - suppression)];
        return SeededPhaseComparison.Series(r);
    }

    // Array-indexed independent native model, instead of TA's odd/even Hilbert rings.
    // Coefficient products and sum order retain the ring implementation's IEEE stages.
    internal static T[][] NativePacked<T>(
        T[] prices,
        double fast,
        double slow,
        int suppression,
        int start,
        int end,
        bool cycleReadings = false,
        int filterStart = 12,
        int baseLookback = 32,
        bool allCycleStages = false,
        bool includeSmooth = false
    )
        where T : IFloatingPointIeee754<T>
    {
        var lookback = (long)baseLookback + suppression;
        if (lookback > end)
            return Enumerable
                .Range(0, cycleReadings ? (includeSmooth ? 4 : 3) : 2)
                .Select(_ => Array.Empty<T>())
                .ToArray();
        start = Math.Max(start, (int)lookback);
        var begin = start - (int)lookback;
        var n = end - begin + 1;
        var a = Enumerable.Range(0, 8).Select(_ => new T[n]).ToArray();
        var period = T.Zero;
        var smoothedPeriod = T.Zero;
        var previousPhase = T.Zero;
        var mama = T.Zero;
        var fama = T.Zero;
        var sum = prices[begin];
        var weighted = sum;
        sum += prices[begin + 1];
        weighted += prices[begin + 1] * T.CreateChecked(2);
        sum += prices[begin + 2];
        weighted += prices[begin + 2] * T.CreateChecked(3);
        var trailing = T.Zero;
        var trailingIndex = begin;
        var result = Enumerable
            .Range(0, cycleReadings ? (includeSmooth ? 4 : 3) : 2)
            .Select(_ => new List<T>())
            .ToArray();
        T Fir(int column, int i, T correction)
        {
            var value = -(T.CreateChecked(.0962) * a[column][i - 6]);
            value += T.CreateChecked(.0962) * a[column][i];
            value -= T.CreateChecked(.5769) * a[column][i - 4];
            value += T.CreateChecked(.5769) * a[column][i - 2];
            return value * correction;
        }
        for (var global = begin + 3; global <= end; global++)
        {
            var price = prices[global];
            sum += price;
            sum -= trailing;
            weighted += price * T.CreateChecked(4);
            trailing = prices[trailingIndex++];
            var smooth = weighted * T.CreateChecked(.1);
            weighted -= sum;
            var i = global - begin;
            if (i < filterStart)
                continue;
            var correction = T.CreateChecked(.075) * period + T.CreateChecked(.54);
            a[0][i] = smooth;
            a[1][i] = Fir(0, i, correction);
            a[2][i] = Fir(1, i, correction);
            a[3][i] = a[1][i - 3];
            var ji = Fir(3, i, correction);
            var jq = Fir(2, i, correction);
            a[4][i] = T.CreateChecked(.2) * (a[3][i] - jq) + T.CreateChecked(.8) * a[4][i - 1];
            a[5][i] = T.CreateChecked(.2) * (a[2][i] + ji) + T.CreateChecked(.8) * a[5][i - 1];
            var phase = !T.IsZero(a[3][i]) ? T.RadiansToDegrees(T.Atan(a[2][i] / a[3][i])) : T.Zero;
            var delta = previousPhase - phase;
            previousPhase = phase;
            if (delta < T.One)
                delta = T.One;
            var alpha =
                delta > T.One
                    ? T.Max(T.CreateChecked(fast) / delta, T.CreateChecked(slow))
                    : T.CreateChecked(fast);
            mama = alpha * price + (T.One - alpha) * mama;
            alpha *= T.CreateChecked(.5);
            fama = alpha * mama + (T.One - alpha) * fama;
            if (global >= start && !cycleReadings)
            {
                result[0].Add(mama);
                result[1].Add(fama);
            }
            a[6][i] =
                T.CreateChecked(.2) * (a[4][i] * a[4][i - 1] + a[5][i] * a[5][i - 1])
                + T.CreateChecked(.8) * a[6][i - 1];
            a[7][i] =
                T.CreateChecked(.2) * (a[4][i] * a[5][i - 1] - a[5][i] * a[4][i - 1])
                + T.CreateChecked(.8) * a[7][i - 1];
            var measured =
                !T.IsZero(a[7][i]) && !T.IsZero(a[6][i])
                    ? T.CreateChecked(360) / T.RadiansToDegrees(T.Atan(a[7][i] / a[6][i]))
                    : period;
            measured = T.Min(measured, T.CreateChecked(1.5) * period);
            measured = T.Max(measured, T.CreateChecked(.67) * period);
            measured = T.Clamp(measured, T.CreateChecked(6), T.CreateChecked(50));
            period = T.CreateChecked(.2) * measured + T.CreateChecked(.8) * period;
            smoothedPeriod = T.CreateChecked(.33) * period + T.CreateChecked(.67) * smoothedPeriod;
            if (cycleReadings && (allCycleStages || global >= start))
            {
                result[0].Add(a[3][i]);
                result[1].Add(a[2][i]);
                result[2].Add(smoothedPeriod);
                if (includeSmooth)
                    result[3].Add(a[0][i]);
            }
        }
        return result.Select(row => row.ToArray()).ToArray();
    }
}

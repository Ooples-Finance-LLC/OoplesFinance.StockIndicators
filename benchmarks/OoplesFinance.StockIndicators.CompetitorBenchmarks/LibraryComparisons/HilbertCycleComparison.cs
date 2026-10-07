using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using TALib;
using TaCore = TALib.Core;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class HilbertCycleComparison
{
    internal static string[] Names(bool periodOnly) =>
        periodOnly ? ["Period"] : ["InPhase", "Quadrature"];

    internal static ComparisonSeries Series(double?[][] values, bool periodOnly) =>
        RetrospectivePriceComparison.Series(Names(periodOnly), values);

    internal static ComparisonPair Pair(bool periodOnly, int suppression = 0) =>
        new(
            periodOnly ? "TaLib.Functions.HtDcPeriod" : "TaLib.Functions.HtPhasor",
            periodOnly ? nameof(HilbertCyclePeriod) : nameof(HilbertPhasor),
            (d, _) => Native(d.Closes, periodOnly),
            (d, _) => Owned(d.IndicatorBars, periodOnly, suppression),
            (d, _) => Series(Reference(d.Closes, periodOnly, suppression), periodOnly),
            Names(periodOnly),
            MinimumInputCount: 2,
            CompetitorReference: (d, _) =>
            {
                var packed = NativePacked(d.Closes, periodOnly, suppression, 0, d.Count - 1);
                var rows = Names(periodOnly).Select(_ => new double?[d.Count]).ToArray();
                for (var j = 0; j < rows.Length; j++)
                for (var i = 0; i < packed[j].Length; i++)
                    rows[j][32 + suppression + i] = packed[j][i];
                return Series(rows, periodOnly);
            },
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal sealed class Settings : IDisposable
    {
        private readonly TaCore.UnstableFunc _kind;
        private readonly int _old;

        internal Settings(bool periodOnly, int suppression)
        {
            _kind = periodOnly ? TaCore.UnstableFunc.HtDcPeriod : TaCore.UnstableFunc.HtPhasor;
            _old = TaCore.UnstablePeriodSettings.Get(_kind);
            TaCore.UnstablePeriodSettings.Set(_kind, suppression);
        }

        public void Dispose() => TaCore.UnstablePeriodSettings.Set(_kind, _old);
    }

    internal static MultiOutputIndicatorBase Indicator(bool periodOnly, int suppression) =>
        periodOnly ? new HilbertCyclePeriod(suppression) : new HilbertPhasor(suppression);

    internal static ComparisonSeries Owned(Bar[] bars, bool periodOnly, int suppression)
    {
        var indicator = Indicator(periodOnly, suppression);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var count = periodOnly ? 1 : 2;
        var rows = Enumerable
            .Range(0, count)
            .Select(j =>
            {
                var values = run[indicator.Outputs[j]].ToArray();
                var flags = run[indicator.Outputs[j + count]].ToArray();
                return values.Select((v, i) => flags[i] > 0 ? (double?)v : null).ToArray();
            })
            .ToArray();
        return Series(rows, periodOnly);
    }

    internal static double?[][] Reference(double[] prices, bool periodOnly, int suppression)
    {
        var rows = SeededPhaseComparison.GridReference(
            prices,
            .5,
            .05,
            false,
            true,
            suppression,
            true
        );
        return periodOnly ? [rows[2]] : [rows[0], rows[1]];
    }

    internal static T[][] NativePacked<T>(
        T[] prices,
        bool periodOnly,
        int suppression,
        int start,
        int end
    )
        where T : IFloatingPointIeee754<T>
    {
        var rows = DelayedPhaseComparison.NativePacked(
            prices,
            .5,
            .05,
            suppression,
            start,
            end,
            true
        );
        return periodOnly ? [rows[2]] : [rows[0], rows[1]];
    }

    internal static TaCore.RetCode NativeCall<T>(
        T[] prices,
        bool periodOnly,
        System.Range range,
        T[] a,
        T[] b,
        out System.Range output
    )
        where T : IFloatingPointIeee754<T> =>
        periodOnly
            ? Functions.HtDcPeriod<T>(prices, range, a, out output)
            : Functions.HtPhasor<T>(prices, range, a, b, out output);

    private static ComparisonSeries Native(double[] prices, bool periodOnly)
    {
        var rows = Names(periodOnly).Select(_ => new double?[prices.Length]).ToArray();
        if (prices.Length == 0)
            return Series(rows, periodOnly);
        var a = new double[prices.Length];
        var b = periodOnly ? Array.Empty<double>() : new double[prices.Length];
        var code = NativeCall(prices, periodOnly, System.Range.All, a, b, out var range);
        if (code != TaCore.RetCode.Success)
            throw new InvalidOperationException("TA Hilbert: " + code);
        for (var i = range.Start.Value; i < range.End.Value; i++)
        {
            rows[0][i] = a[i - range.Start.Value];
            if (!periodOnly)
                rows[1][i] = b[i - range.Start.Value];
        }
        return Series(rows, periodOnly);
    }
}

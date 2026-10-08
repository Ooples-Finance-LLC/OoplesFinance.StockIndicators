using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using TALib;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;
using TaCore = TALib.Core;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class HilbertCycleSignalComparison
{
    internal static string[] Names(int kind) =>
        kind == 0 ? ["Phase"]
        : kind == 1 ? ["Sine", "LeadSine"]
        : ["Trend"];

    internal static MultiOutputIndicatorBase Indicator(int kind, int suppression) =>
        kind == 0 ? new DelayedHilbertPhase(suppression)
        : kind == 1 ? new DelayedHilbertSine(suppression)
        : new DelayedHilbertTrendMode(suppression);

    internal static ComparisonSeries Series(double?[][] values, int kind) =>
        RetrospectivePriceComparison.Series(Names(kind), values);

    internal static ComparisonPair Pair(int kind, int suppression = 0) =>
        new(
            "TaLib.Functions."
                + (
                    kind == 0 ? "HtDcPhase"
                    : kind == 1 ? "HtSine"
                    : "HtTrendMode"
                ),
            kind == 0 ? nameof(DelayedHilbertPhase)
                : kind == 1 ? nameof(DelayedHilbertSine)
                : nameof(DelayedHilbertTrendMode),
            (d, _) => Native(d.Closes, kind),
            (d, _) => Owned(d.IndicatorBars, kind, suppression),
            (d, _) => Series(Reference(d.Closes, kind, suppression), kind),
            Names(kind),
            MinimumInputCount: 2,
            CompetitorReference: (d, _) =>
            {
                var packed = NativePacked(d.Closes, kind, suppression, 0, d.Count - 1);
                var rows = Names(kind).Select(_ => new double?[d.Count]).ToArray();
                for (var j = 0; j < rows.Length; j++)
                for (var i = 0; i < packed[j].Length; i++)
                    rows[j][63 + suppression + i] = packed[j][i];
                return Series(rows, kind);
            },
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal sealed class Settings : IDisposable
    {
        private readonly TaCore.UnstableFunc _kind;
        private readonly int _old;

        internal Settings(int kind, int suppression)
        {
            _kind =
                kind == 0 ? TaCore.UnstableFunc.HtDcPhase
                : kind == 1 ? TaCore.UnstableFunc.HtSine
                : TaCore.UnstableFunc.HtTrendMode;
            _old = TaCore.UnstablePeriodSettings.Get(_kind);
            TaCore.UnstablePeriodSettings.Set(_kind, suppression);
        }

        public void Dispose() => TaCore.UnstablePeriodSettings.Set(_kind, _old);
    }

    internal static ComparisonSeries Owned(Bar[] bars, int kind, int suppression)
    {
        var owner = Indicator(kind, suppression);
        var count = Names(kind).Length;
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(owner)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return Series(
            Enumerable
                .Range(0, count)
                .Select(j =>
                {
                    var values = run[owner.Outputs[j]].ToArray();
                    var flags = run[owner.Outputs[j + count]].ToArray();
                    return values.Select((v, i) => flags[i] > 0 ? (double?)v : null).ToArray();
                })
                .ToArray(),
            kind
        );
    }

    internal static TaCore.RetCode NativeCall<T>(
        T[] prices,
        int kind,
        System.Range range,
        T[] a,
        T[] b,
        int[] trend,
        out System.Range output
    )
        where T : IFloatingPointIeee754<T> =>
        kind == 0 ? Functions.HtDcPhase<T>(prices, range, a, out output)
        : kind == 1 ? Functions.HtSine<T>(prices, range, a, b, out output)
        : Functions.HtTrendMode<T>(prices, range, trend, out output);

    private static ComparisonSeries Native(double[] prices, int kind)
    {
        var rows = Names(kind).Select(_ => new double?[prices.Length]).ToArray();
        if (prices.Length == 0)
            return Series(rows, kind);
        var a = kind == 2 ? Array.Empty<double>() : new double[prices.Length];
        var b = kind == 1 ? new double[prices.Length] : Array.Empty<double>();
        var trend = kind == 2 ? new int[prices.Length] : Array.Empty<int>();
        var code = NativeCall(prices, kind, System.Range.All, a, b, trend, out var range);
        if (code != TaCore.RetCode.Success)
            throw new InvalidOperationException("TA Hilbert signal: " + code);
        for (var i = range.Start.Value; i < range.End.Value; i++)
        {
            var j = i - range.Start.Value;
            rows[0][i] = kind == 2 ? trend[j] : a[j];
            if (kind == 1)
                rows[1][i] = b[j];
        }
        return Series(rows, kind);
    }

    internal static double?[][] Reference(double[] prices, int kind, int suppression)
    {
        var result = Names(kind).Select(_ => new double?[prices.Length]).ToArray();
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
        var sm = new BigInteger[prices.Length];
        var means = new BigInteger[prices.Length];
        double phase = 0,
            sine = 0,
            lead = 0;
        long days = 0;
        for (var i = 37; i < prices.Length; i++)
        {
            sm[i] = DirectionalComparison.RoundedUnits(
                4 * Units(prices[i])
                    + 3 * Units(prices[i - 1])
                    + 2 * Units(prices[i - 2])
                    + Units(prices[i - 3]),
                10
            );
            var period = periods[i]!.Value;
            var count = (int)(period + .5);
            BigInteger real = 0,
                imaginary = 0;
            for (var lag = 0; lag < count; lag++)
            {
                var angle = lag * 2d * Math.PI / count;
                real += Units(Math.Sin(angle)) * sm[i - lag];
                imaginary += Units(Math.Cos(angle)) * sm[i - lag];
            }
            real = DirectionalComparison.RoundedUnits(real, Grid);
            imaginary = DirectionalComparison.RoundedUnits(imaginary, Grid);
            var next = imaginary.IsZero
                ? phase
                    + (
                        real.Sign < 0 ? -90
                        : real.Sign > 0 ? 90
                        : 0
                    )
                : Math.Atan(Round(real, imaginary)) * 180 / Math.PI;
            next += 90;
            next += 360 / period;
            if (imaginary.Sign < 0)
                next += 180;
            if (next > 315)
                next -= 360;
            var sn = Math.Sin(next * (Math.PI / 180));
            var ld = Math.Sin((next + 45) * (Math.PI / 180));
            var trend = 1;
            if (kind == 2)
            {
                if (count > 0)
                    means[i] = DirectionalComparison.RoundedUnits(
                        Enumerable
                            .Range(i - count + 1, count)
                            .Aggregate(BigInteger.Zero, (s, j) => s + Units(prices[j])),
                        count
                    );
                var baseline = DirectionalComparison.RoundedUnits(
                    4 * means[i] + 3 * means[i - 1] + 2 * means[i - 2] + means[i - 3],
                    10
                );
                if (sn > ld && sine <= lead || sn < ld && sine >= lead)
                {
                    days = 0;
                    trend = 0;
                }
                if (++days < .5 * period)
                    trend = 0;
                if (next - phase > .67 * 90 * 4 / period && next - phase < 1.5 * 90 * 4 / period)
                    trend = 0;
                if (
                    !baseline.IsZero
                    && BigInteger.Abs(sm[i] - baseline) * Grid
                        >= Units(.015) * BigInteger.Abs(baseline)
                )
                    trend = 1;
            }
            phase = next;
            sine = sn;
            lead = ld;
            if (i < 63L + suppression)
                continue;
            result[0][i] =
                kind == 0 ? phase
                : kind == 1 ? sine
                : trend;
            if (kind == 1)
                result[1][i] = lead;
        }
        return result;
    }

    internal static T[][] NativePacked<T>(T[] prices, int kind, int suppression, int start, int end)
        where T : IFloatingPointIeee754<T>
    {
        var rows = Names(kind).Select(_ => new List<T>()).ToArray();
        var lookback = 63L + suppression;
        if (lookback > end)
            return rows.Select(r => r.ToArray()).ToArray();
        start = Math.Max(start, (int)lookback);
        var begin = start - (int)lookback;
        var stages = DelayedPhaseComparison.NativePacked(
            prices,
            .5,
            .05,
            suppression,
            start,
            end,
            true,
            37,
            63,
            true,
            true
        );
        var period = stages[2];
        var smooth = stages[3];
        var means = new T[period.Length + 3];
        var phase = T.Zero;
        var sine = T.Zero;
        var lead = T.Zero;
        long days = 0;
        for (var i = 0; i < period.Length; i++)
        {
            var count = int.CreateTruncating(period[i] + T.CreateChecked(.5));
            var real = T.Zero;
            var imaginary = T.Zero;
            for (var lag = 0; lag < count; lag++)
            {
                var angle =
                    T.CreateChecked(lag) * T.CreateChecked(2) * T.Pi / T.CreateChecked(count);
                var price = i >= lag ? smooth[i - lag] : T.Zero;
                real += T.Sin(angle) * price;
                imaginary += T.Cos(angle) * price;
            }
            var next = T.Zero;
            if (T.Abs(imaginary) > T.Zero)
                next = T.RadiansToDegrees(T.Atan(real / imaginary));
            else if (T.Abs(imaginary) <= T.CreateChecked(.01))
                next =
                    phase
                    + (
                        real < T.Zero ? T.CreateChecked(-90)
                        : real > T.Zero ? T.CreateChecked(90)
                        : T.Zero
                    );
            next += T.CreateChecked(90);
            next += T.CreateChecked(360) / period[i];
            if (imaginary < T.Zero)
                next += T.CreateChecked(180);
            if (next > T.CreateChecked(315))
                next -= T.CreateChecked(360);
            var sn = T.Sin(T.DegreesToRadians(next));
            var ld = T.Sin(T.DegreesToRadians(next + T.CreateChecked(45)));
            var trend = 1;
            if (kind == 2)
            {
                var sum = T.Zero;
                for (var lag = 0; lag < count; lag++)
                    sum += prices[begin + 37 + i - lag];
                means[i + 3] = count > 0 ? sum / T.CreateChecked(count) : T.Zero;
                var baseline =
                    (
                        T.CreateChecked(4) * means[i + 3]
                        + T.CreateChecked(3) * means[i + 2]
                        + T.CreateChecked(2) * means[i + 1]
                        + means[i]
                    ) / T.CreateChecked(10);
                if (sn > ld && sine <= lead || sn < ld && sine >= lead)
                {
                    days = 0;
                    trend = 0;
                }
                if (T.CreateChecked(++days) < T.CreateChecked(.5) * period[i])
                    trend = 0;
                var delta = next - phase;
                if (
                    !T.IsZero(period[i])
                    && delta
                        > T.CreateChecked(.67)
                            * T.CreateChecked(90)
                            * T.CreateChecked(4)
                            / period[i]
                    && delta
                        < T.CreateChecked(1.5)
                            * T.CreateChecked(90)
                            * T.CreateChecked(4)
                            / period[i]
                )
                    trend = 0;
                if (
                    !T.IsZero(baseline)
                    && T.Abs((smooth[i] - baseline) / baseline) >= T.CreateChecked(.015)
                )
                    trend = 1;
            }
            phase = next;
            sine = sn;
            lead = ld;
            if (begin + 37 + i < start)
                continue;
            rows[0]
                .Add(
                    kind == 0 ? phase
                    : kind == 1 ? sine
                    : T.CreateChecked(trend)
                );
            if (kind == 1)
                rows[1].Add(lead);
        }
        return rows.Select(r => r.ToArray()).ToArray();
    }
}

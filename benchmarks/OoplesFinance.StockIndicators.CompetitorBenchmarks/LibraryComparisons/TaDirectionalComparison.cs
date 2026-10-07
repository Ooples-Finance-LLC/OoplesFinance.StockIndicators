using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using TALib;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;
using TaCore = TALib.Core;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class TaDirectionalComparison
{
    internal static readonly string[] Names =
    [
        "PlusDM",
        "MinusDM",
        "PlusDI",
        "MinusDI",
        "Dx",
        "Adx",
        "Adxr",
    ];
    internal static readonly ComparisonPair[] Pairs = Enum.GetValues<PriorDirectionalMeasure>()
        .Select(m => Pair(m))
        .ToArray();

    internal static ComparisonPair Pair(PriorDirectionalMeasure measure, int unstable = 0) =>
        new(
            "TaLib.Functions." + Names[(int)measure],
            nameof(PriorSeededDirectionalMeasure),
            (d, p) => Native(d, p, measure, unstable),
            (d, p) => Owned(d.IndicatorBars, p, measure, unstable),
            (d, p) => VolumePriceComparison.Mask(Reference(d.IndicatorBars, p, measure, unstable)),
            MinimumInputCount: 2,
            CompetitorReference: (d, p) =>
                VolumePriceComparison.Mask(NativeReference(d.IndicatorBars, p, measure, unstable)),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static TaCore.UnstableFunc Setting(PriorDirectionalMeasure m) =>
        m switch
        {
            PriorDirectionalMeasure.PositiveMovement => TaCore.UnstableFunc.PlusDM,
            PriorDirectionalMeasure.NegativeMovement => TaCore.UnstableFunc.MinusDM,
            PriorDirectionalMeasure.PositiveIndicator => TaCore.UnstableFunc.PlusDI,
            PriorDirectionalMeasure.NegativeIndicator => TaCore.UnstableFunc.MinusDI,
            PriorDirectionalMeasure.Index => TaCore.UnstableFunc.Dx,
            _ => TaCore.UnstableFunc.Adx,
        };

    internal sealed class Settings(PriorDirectionalMeasure measure, int value) : IDisposable
    {
        private readonly TaCore.UnstableFunc _setting = Setting(measure);
        private readonly int _old = Set(measure, value);

        private static int Set(PriorDirectionalMeasure m, int value)
        {
            var old = TaCore.UnstablePeriodSettings.Get(Setting(m));
            TaCore.UnstablePeriodSettings.Set(Setting(m), value);
            return old;
        }

        public void Dispose() => TaCore.UnstablePeriodSettings.Set(_setting, _old);
    }

    internal static long Lookback(int p, PriorDirectionalMeasure m, int unstable) =>
        p == 1 ? 1
        : m <= PriorDirectionalMeasure.NegativeMovement ? (long)p - 1 + unstable
        : m <= PriorDirectionalMeasure.Index ? (long)p + unstable
        : m == PriorDirectionalMeasure.Average ? 2L * p - 1 + unstable
        : 3L * p - 2 + unstable;

    internal static int NativeLookback(int p, PriorDirectionalMeasure m) =>
        m switch
        {
            PriorDirectionalMeasure.PositiveMovement => Functions.PlusDMLookback(p),
            PriorDirectionalMeasure.NegativeMovement => Functions.MinusDMLookback(p),
            PriorDirectionalMeasure.PositiveIndicator => Functions.PlusDILookback(p),
            PriorDirectionalMeasure.NegativeIndicator => Functions.MinusDILookback(p),
            PriorDirectionalMeasure.Index => Functions.DxLookback(p),
            PriorDirectionalMeasure.Average => Functions.AdxLookback(p),
            _ => Functions.AdxrLookback(p),
        };

    internal static TaCore.RetCode Call<T>(
        T[] h,
        T[] l,
        T[] c,
        System.Range range,
        T[] output,
        out System.Range result,
        int p,
        PriorDirectionalMeasure m
    )
        where T : IFloatingPointIeee754<T> =>
        m switch
        {
            PriorDirectionalMeasure.PositiveMovement => Functions.PlusDM<T>(
                h,
                l,
                range,
                output,
                out result,
                p
            ),
            PriorDirectionalMeasure.NegativeMovement => Functions.MinusDM<T>(
                h,
                l,
                range,
                output,
                out result,
                p
            ),
            PriorDirectionalMeasure.PositiveIndicator => Functions.PlusDI<T>(
                h,
                l,
                c,
                range,
                output,
                out result,
                p
            ),
            PriorDirectionalMeasure.NegativeIndicator => Functions.MinusDI<T>(
                h,
                l,
                c,
                range,
                output,
                out result,
                p
            ),
            PriorDirectionalMeasure.Index => Functions.Dx<T>(h, l, c, range, output, out result, p),
            PriorDirectionalMeasure.Average => Functions.Adx<T>(
                h,
                l,
                c,
                range,
                output,
                out result,
                p
            ),
            _ => Functions.Adxr<T>(h, l, c, range, output, out result, p),
        };

    private static ComparisonSeries Native(
        CompetitorData data,
        int p,
        PriorDirectionalMeasure m,
        int unstable
    )
    {
        if (TaCore.UnstablePeriodSettings.Get(Setting(m)) != unstable)
            throw new InvalidOperationException("Directional comparison settings mismatch");
        var values = new double?[data.Count];
        if (data.Count == 0)
            return VolumePriceComparison.Mask(values);
        var buffer = new double[data.Count];
        var code = Call(
            data.Highs,
            data.Lows,
            data.Closes,
            System.Range.All,
            buffer,
            out var range,
            p,
            m
        );
        if (code != TaCore.RetCode.Success)
            throw new InvalidOperationException("Directional native error: " + code);
        var start = (int)Math.Min(data.Count, Lookback(p, m, unstable));
        var count = data.Count - start;
        // Native DX returns packed offsets, verified directly in the API regression.
        // Validate that defect before placing values on their calculation bars.
        var expectedStart = count == 0 || m == PriorDirectionalMeasure.Index ? 0 : start;
        if (!range.Equals(new System.Range(expectedStart, expectedStart + count)))
            throw new InvalidOperationException("Unexpected directional native range");
        for (var i = 0; i < count; i++)
            values[start + i] = buffer[i];
        return VolumePriceComparison.Mask(values);
    }

    internal static ComparisonSeries Owned(
        Bar[] bars,
        int p,
        PriorDirectionalMeasure measure,
        int unstable = 0
    )
    {
        var indicator = new PriorSeededDirectionalMeasure(measure, p, unstable);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var flags = run[indicator.IsDefined].ToArray();
        var values = run[indicator.Value].ToArray();
        return VolumePriceComparison.Mask(
            values.Select((v, i) => flags[i] > 0 ? (double?)v : null).ToArray()
        );
    }

    internal static double?[] Reference(
        Bar[] bars,
        int p,
        PriorDirectionalMeasure m,
        int unstable = 0
    )
    {
        var n = bars.Length;
        var result = new double?[n];
        var averages = new double?[n];
        var dxs = new double[n];
        var raw = Enumerable.Range(0, 3).Select(_ => new BigInteger[n]).ToArray();
        var sums = new BigInteger[3];
        var first = Lookback(p, m, unstable);
        var averageStart = 2L * p - 1 + unstable;
        double average = 0,
            lastDx = 0;
        BigInteger R(BigInteger x, BigInteger d) => DirectionalComparison.RoundedUnits(x, d);
        for (var i = 1; i < n; i++)
        {
            var up = Units(bars[i].High) - Units(bars[i - 1].High);
            var down = Units(bars[i - 1].Low) - Units(bars[i].Low);
            raw[0][i] = R(up > 0 && up > down ? up : 0, 1);
            raw[1][i] = R(down > 0 && down > up ? down : 0, 1);
            raw[2][i] = R(
                new[]
                {
                    Units(bars[i].High) - Units(bars[i].Low),
                    BigInteger.Abs(Units(bars[i].High) - Units(bars[i - 1].Close)),
                    BigInteger.Abs(Units(bars[i].Low) - Units(bars[i - 1].Close)),
                }.Max(),
                1
            );
            if (p == 1)
            {
                result[i] =
                    m <= PriorDirectionalMeasure.NegativeMovement ? Round(raw[(int)m % 2][i], Grid)
                    : raw[2][i].IsZero ? 0
                    : Round(raw[(int)m % 2][i], raw[2][i]);
                continue;
            }
            if (i < p - 1)
                continue;
            for (var j = 0; j < 3; j++)
                sums[j] =
                    i == p - 1
                        ? R(
                            raw[j].Skip(1).Take(p - 1).Aggregate(BigInteger.Zero, (a, b) => a + b),
                            1
                        )
                        : R(sums[j] * (p - 1) + raw[j][i] * p, p);
            if (m <= PriorDirectionalMeasure.NegativeMovement)
            {
                if (i >= first)
                    result[i] = Round(sums[(int)m], Grid);
                continue;
            }
            if (i < p)
                continue;
            if (m == PriorDirectionalMeasure.Index && i > first)
            {
                var current = R(
                    new[]
                    {
                        Units(bars[i].High) - Units(bars[i].Low),
                        BigInteger.Abs(Units(bars[i].High) - Units(bars[i].Close)),
                        BigInteger.Abs(Units(bars[i].Low) - Units(bars[i].Close)),
                    }.Max(),
                    1
                );
                sums[2] = R(sums[2] * (p - 1) + current * p, p);
            }
            if (
                m
                is PriorDirectionalMeasure.PositiveIndicator
                    or PriorDirectionalMeasure.NegativeIndicator
            )
            {
                if (i >= first)
                    result[i] = sums[2].IsZero ? 0 : Round(100 * sums[(int)m % 2], sums[2]);
                continue;
            }
            var plus = sums[2].IsZero ? BigInteger.Zero : R(100 * sums[0] * Grid, sums[2]);
            var minus = sums[2].IsZero ? BigInteger.Zero : R(100 * sums[1] * Grid, sums[2]);
            var has = !(plus + minus).IsZero;
            dxs[i] = has ? Round(100 * BigInteger.Abs(plus - minus), plus + minus) : 0;
            if (m == PriorDirectionalMeasure.Index)
            {
                if (i >= first)
                {
                    if (i == first || has)
                        lastDx = dxs[i];
                    result[i] = lastDx;
                }
                continue;
            }
            if (i == 2L * p - 1)
                average = Round(
                    dxs.Skip(p).Take(p).Aggregate(BigInteger.Zero, (s, v) => s + Units(v)),
                    Grid * p
                );
            else if (i >= 2L * p && has)
                average = Round(Units(average) * (p - 1) + Units(dxs[i]), Grid * p);
            if (i < averageStart)
                continue;
            averages[i] = average;
            if (i >= first)
                result[i] =
                    m == PriorDirectionalMeasure.Average
                        ? average
                        : Round(Units(average) + Units(averages[i - p + 1]!.Value), 2 * Grid);
        }
        return result;
    }

    internal static double?[] NativeReference(
        Bar[] bars,
        int p,
        PriorDirectionalMeasure m,
        int unstable = 0
    )
    {
        var n = bars.Length;
        var result = new double?[n];
        var averages = new double?[n];
        var raw = Enumerable.Range(0, 3).Select(_ => new double[n]).ToArray();
        var sums = new double[3];
        var first = Lookback(p, m, unstable);
        double average = 0,
            seed = 0,
            lastDx = 0;
        for (var i = 1; i < n; i++)
        {
            var up = Subtract(bars[i].High, bars[i - 1].High);
            var down = Subtract(bars[i - 1].Low, bars[i].Low);
            raw[0][i] = up > 0 && up > down ? up : 0;
            raw[1][i] = down > 0 && down > up ? down : 0;
            raw[2][i] = Math.Max(
                Subtract(bars[i].High, bars[i].Low),
                Math.Max(
                    Math.Abs(Subtract(bars[i].High, bars[i - 1].Close)),
                    Math.Abs(Subtract(bars[i].Low, bars[i - 1].Close))
                )
            );
            if (p == 1)
            {
                result[i] =
                    m <= PriorDirectionalMeasure.NegativeMovement ? raw[(int)m % 2][i]
                    : raw[2][i] == 0 ? 0 // NOSONAR: Native exact zero range.
                    : Divide(raw[(int)m % 2][i], raw[2][i]);
                continue;
            }
            if (i < p - 1)
                continue;
            for (var j = 0; j < 3; j++)
                sums[j] =
                    i == p - 1
                        ? raw[j].Skip(1).Take(p - 1).Aggregate(0d, Add)
                        : Add(Subtract(sums[j], Divide(sums[j], p)), raw[j][i]);
            if (m <= PriorDirectionalMeasure.NegativeMovement)
            {
                if (i >= first)
                    result[i] = sums[(int)m];
                continue;
            }
            if (i < p)
                continue;
            if (m == PriorDirectionalMeasure.Index && i > first)
            {
                var current = Math.Max(
                    Subtract(bars[i].High, bars[i].Low),
                    Math.Max(
                        Math.Abs(Subtract(bars[i].High, bars[i].Close)),
                        Math.Abs(Subtract(bars[i].Low, bars[i].Close))
                    )
                );
                sums[2] = Add(Subtract(sums[2], Divide(sums[2], p)), current);
            }
            if (
                m
                is PriorDirectionalMeasure.PositiveIndicator
                    or PriorDirectionalMeasure.NegativeIndicator
            )
            {
                if (i >= first)
                    result[i] = sums[2] == 0 ? 0 : Multiply(100, Divide(sums[(int)m % 2], sums[2])); // NOSONAR: Native exact zero range.
                continue;
            }
            var plus = sums[2] == 0 ? 0 : Multiply(100, Divide(sums[0], sums[2])); // NOSONAR: Native exact zero range.
            var minus = sums[2] == 0 ? 0 : Multiply(100, Divide(sums[1], sums[2])); // NOSONAR: Native exact zero range.
            var total = Add(plus, minus);
            var has = total != 0; // NOSONAR: Native exact zero direction.
            var dx = has ? Multiply(100, Divide(Math.Abs(Subtract(minus, plus)), total)) : 0;
            if (m == PriorDirectionalMeasure.Index)
            {
                if (i >= first)
                {
                    if (i == first || has)
                        lastDx = dx;
                    result[i] = lastDx;
                }
                continue;
            }
            if (i < 2L * p)
            {
                seed = Add(seed, dx);
                if (i == 2L * p - 1)
                    average = Divide(seed, p);
            }
            else if (has)
                average = Divide(Add(Multiply(average, p - 1), dx), p);
            if (i < 2L * p - 1 + unstable)
                continue;
            averages[i] = average;
            if (i >= first)
                result[i] =
                    m == PriorDirectionalMeasure.Average
                        ? average
                        : Divide(Add(average, averages[i - p + 1]!.Value), 2);
        }
        return result;
    }
}

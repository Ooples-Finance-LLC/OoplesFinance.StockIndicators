using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using TALib;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;
using TaCore = TALib.Core;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class ClassicAverageComparison
{
    internal static ComparisonPair Pair(
        ClassicAverageMethod method = ClassicAverageMethod.Sma,
        bool first = false,
        int suppression = 0
    ) =>
        new(
            "TaLib.Functions.Ma",
            nameof(ClassicMovingAverage),
            (d, p) => Native(d, p, method),
            (d, p) => Owned(d.IndicatorBars, p, method, first, suppression),
            (d, p) =>
                VolumePriceComparison.Mask(Reference(d.Closes, p, method, first, suppression)),
            MinimumInputCount: 2,
            CompetitorReference: (d, p) => NativeReference(d.Closes, p, method, first, suppression),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal sealed class Settings : IDisposable
    {
        private readonly TripleRateComparison.Settings _ema;
        private readonly TaAdaptiveComparison.Settings _kama;
        private readonly DelayedPhaseComparison.Settings _mama;
        private readonly int _t3 = TaCore.UnstablePeriodSettings.Get(TaCore.UnstableFunc.T3);

        internal Settings(bool first, int suppression)
        {
            _ema = new(first, suppression);
            _kama = new(suppression);
            _mama = new(suppression);
            TaCore.UnstablePeriodSettings.Set(TaCore.UnstableFunc.T3, suppression);
        }

        public void Dispose()
        {
            TaCore.UnstablePeriodSettings.Set(TaCore.UnstableFunc.T3, _t3);
            _mama.Dispose();
            _kama.Dispose();
            _ema.Dispose();
        }
    }

    internal static long First(int p, ClassicAverageMethod method, int suppression) =>
        p == 1
            ? 0
            : method switch
            {
                ClassicAverageMethod.Ema => p - 1L + suppression,
                ClassicAverageMethod.Dema => 2 * (p - 1L + suppression),
                ClassicAverageMethod.Tema => 3 * (p - 1L + suppression),
                ClassicAverageMethod.Kama => (long)p + suppression,
                ClassicAverageMethod.Mama => 32L + suppression,
                ClassicAverageMethod.T3 => 6L * (p - 1) + suppression,
                _ => p - 1L,
            };

    internal static ComparisonSeries Owned(
        Bar[] bars,
        int p,
        ClassicAverageMethod method,
        bool first,
        int suppression
    )
    {
        var indicator = new ClassicMovingAverage(p, method, first, suppression);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var values = run[indicator.Average].ToArray();
        var flags = run[indicator.IsDefined].ToArray();
        return VolumePriceComparison.Mask(
            values.Select((v, i) => flags[i] > 0 ? (double?)v : null).ToArray()
        );
    }

    private static ComparisonSeries Native(CompetitorData d, int p, ClassicAverageMethod method)
    {
        var result = new double?[d.Count];
        if (d.Count == 0)
            return VolumePriceComparison.Mask(result);
        var output = new double[d.Count];
        var code = Functions.Ma<double>(
            d.Closes,
            System.Range.All,
            output,
            out var range,
            p,
            (TaCore.MAType)method
        );
        if (code != TaCore.RetCode.Success)
            throw new InvalidOperationException("TA MA: " + code);
        for (var i = range.Start.Value; i < range.End.Value; i++)
            result[i] = output[i - range.Start.Value];
        return VolumePriceComparison.Mask(result);
    }

    private static ComparisonSeries NativeReference(
        double[] input,
        int p,
        ClassicAverageMethod method,
        bool first,
        int suppression
    )
    {
        var result = new double?[input.Length];
        var packed = ClassicNativeReference.Packed(
            input,
            p,
            method,
            first,
            suppression,
            0,
            input.Length - 1
        );
        var start = First(p, method, suppression);
        for (var i = start; i < input.Length; i++)
            result[(int)i] = packed[(int)(i - start)];
        return VolumePriceComparison.Mask(result);
    }

    internal static double?[] Reference(
        double[] input,
        int p,
        ClassicAverageMethod method,
        bool first,
        int suppression
    )
    {
        if (p == 1)
            return input.Select(v => (double?)v).ToArray();
        if (method == ClassicAverageMethod.Kama)
            return SeededAdaptiveComparison.ReferenceValues(
                input,
                p,
                2,
                30,
                false,
                false,
                false,
                true,
                1L + suppression
            )[0];
        if (method == ClassicAverageMethod.Mama)
            return SeededPhaseComparison.GridReference(input, .5, .05, false, true, suppression)[0];
        if (method == ClassicAverageMethod.T3)
            return TillsonComparison.Reference(
                input,
                p,
                .7,
                TillsonSeed.CascadedMeans,
                suppression
            );
        if (
            method
            is ClassicAverageMethod.Ema
                or ClassicAverageMethod.Dema
                or ClassicAverageMethod.Tema
        )
            return Exponential(input, p, method, first, suppression)
                .Select(v => v.HasValue ? (double?)Round(v.Value, Grid) : null)
                .ToArray();
        var result = new double?[input.Length];
        for (var i = p - 1; i < input.Length; i++)
        {
            var total = BigInteger.Zero;
            long mass = 0;
            for (var lag = 0; lag < p; lag++)
            {
                var weight =
                    method == ClassicAverageMethod.Wma ? p - lag
                    : method == ClassicAverageMethod.Trima ? Math.Min(lag + 1, p - lag)
                    : 1;
                total += weight * Units(input[i - lag]);
                mass += weight;
            }
            result[i] = Round(total, mass * Grid);
        }
        return result;
    }

    internal static BigInteger?[] ExtendedReference(
        double[] input,
        int p,
        ClassicAverageMethod method,
        bool first,
        int suppression
    )
    {
        if (
            p != 1
            && method
                is ClassicAverageMethod.Ema
                    or ClassicAverageMethod.Dema
                    or ClassicAverageMethod.Tema
        )
            return Exponential(input, p, method, first, suppression);
        if (p != 1 && method == ClassicAverageMethod.T3)
            return TillsonComparison.ExtendedReference(
                input,
                p,
                .7,
                TillsonSeed.CascadedMeans,
                suppression
            );
        return Reference(input, p, method, first, suppression)
            .Select(v => v.HasValue ? (BigInteger?)Units(v.Value) : null)
            .ToArray();
    }

    internal static BigInteger?[] AlignedExtendedReference(
        double[] input,
        int p,
        ClassicAverageMethod method,
        bool first,
        int suppression,
        long start
    )
    {
        var offset = start - First(p, method, suppression);
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(start));
        if (start >= input.Length)
            return new BigInteger?[input.Length];
        if (
            p != 1
            && method
                is ClassicAverageMethod.Ema
                    or ClassicAverageMethod.Dema
                    or ClassicAverageMethod.Tema
        )
            return Exponential(input, p, method, first, suppression, offset);
        return Enumerable
            .Repeat<BigInteger?>(null, (int)offset)
            .Concat(
                ExtendedReference(input.Skip((int)offset).ToArray(), p, method, first, suppression)
            )
            .ToArray();
    }

    private static BigInteger?[] Exponential(
        double[] input,
        int p,
        ClassicAverageMethod method,
        bool first,
        int suppression,
        long offset = 0
    ) => ExponentialUnits(input.Select(Units).ToArray(), p, method, first, suppression, offset);

    internal static BigInteger?[] ExponentialUnits(
        BigInteger[] input,
        int p,
        ClassicAverageMethod method,
        bool first,
        int suppression,
        long offset = 0
    )
    {
        var order =
            method == ClassicAverageMethod.Ema ? 1
            : method == ClassicAverageMethod.Dema ? 2
            : 3;
        var layers = Enumerable.Range(0, order).Select(_ => new BigInteger[input.Length]).ToArray();
        var delay = p - 1L + suppression;
        for (var j = 0; j < order; j++)
        {
            var start = first && j == 0 ? 0 : offset + j * delay;
            if (start >= input.Length)
                continue;
            BigInteger Input(int i) => j == 0 ? input[i] : layers[j - 1][i];
            var seed = BigInteger.Zero;
            for (var i = (int)start; i < input.Length; i++)
            {
                if (!first && i < start + p)
                {
                    seed += Input(i);
                    if (i == start + p - 1)
                        layers[j][i] = DirectionalComparison.RoundedUnits(seed, p);
                }
                else
                    layers[j][i] =
                        i == start
                            ? Input(i)
                            : DirectionalComparison.RoundedUnits(
                                2 * Input(i) + (p - 1L) * layers[j][i - 1],
                                p + 1L
                            );
            }
        }
        var result = new BigInteger?[input.Length];
        for (var i = (int)Math.Min(input.Length, offset + order * delay); i < input.Length; i++)
            result[i] = DirectionalComparison.RoundedUnits(
                order == 1 ? layers[0][i]
                    : order == 2 ? 2 * layers[0][i] - layers[1][i]
                    : 3 * (layers[0][i] - layers[1][i]) + layers[2][i],
                1
            );
        return result;
    }
}

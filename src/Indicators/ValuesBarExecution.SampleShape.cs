#if !NETFRAMEWORK
using System.Buffers;
using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static PointwiseRegion ComputeSampleShapeColumn(Bar[] source, double[][] output, OwnedBarBuffer? owned,
        int start, int end, WindowSampleShape indicator, ReadOnlySpan<Bar> seed, ReadOnlySpan<Bar> tail,
        CancellationToken cancellation)
    {
        var region = new PointwiseRegion { InvalidOutputIndex = -1 };
        double[] seedCloses = seed.IsEmpty ? [] : ArrayPool<double>.Shared.Rent(seed.Length);
        try
        {
            int grid = int.MaxValue;
            double maximum = 0;
            int count = seed.Length + end - start;
            for (int i = 0; i < count; i++)
            {
                if (cancellation.IsCancellationRequested) return region;
                int row = start + i - seed.Length;
                var bar = i < seed.Length ? seed[i]
                    : row >= end - tail.Length ? tail[row - (end - tail.Length)] : source[row];
                if (!AllFieldsFinite(in bar))
                { region.InvalidInput = true; region.Last = bar; return region; }
                if (i < seed.Length) seedCloses[i] = bar.Close;
                else
                {
                    region.Last = bar;
                    if (owned is not null) owned[row] = bar;
                    // Presence temporarily retains the captured close. The
                    // fixed-size integer ring retains observations after publication.
                    output[1][row] = bar.Close;
                }
                IncludeIntegerGrid(bar.Close, ref grid, ref maximum);
            }
            if (grid == int.MaxValue) grid = 0;
            if (Math.ScaleB(maximum, -grid) <= 4096)
                FillSampleShapeGrid(output, start, end, seedCloses.AsSpan(0, seed.Length),
                    indicator, grid, cancellation);
            else
                ReplayCapturedSampleShape(output, start, end, seedCloses.AsSpan(0, seed.Length),
                    indicator, cancellation);
            return region;
        }
        finally { if (!seed.IsEmpty) ArrayPool<double>.Shared.Return(seedCloses); }
    }

    private static void ReplayCapturedSampleShape(double[][] output, int start, int end,
        ReadOnlySpan<double> seed, WindowSampleShape indicator, CancellationToken cancellation)
    {
        var state = new WindowSampleShape.State(indicator.Period, indicator.Kurtosis);
        Span<double> values = stackalloc double[2];
        for (int i = 0; i < seed.Length + end - start; i++)
        {
            if (cancellation.IsCancellationRequested) return;
            int row = start + i - seed.Length;
            var bar = new Bar(default, 0, 0, 0, i < seed.Length ? seed[i] : output[1][row], 0);
            state.Update(in bar, values);
            if (i < seed.Length) continue;
            output[0][row] = values[0]; output[1][row] = values[1];
        }
    }

    private static void FillSampleShapeGrid(double[][] output, int start, int end,
        ReadOnlySpan<double> seed, WindowSampleShape indicator, int grid, CancellationToken cancellation)
    {
        // P<=64 and |integer|<=4096 bound every raw moment by 2^54.
        // The largest skew intermediate is b^2*n*(n-1), bounded by
        // 36*n^8*M^6 <= 36*2^120 < 2^126. Kurtosis intermediates are
        // bounded by 17*n^6*M^4 <= 17*2^84 < 2^89. Signed Int128 suffices
        // before cancellation; no bound relies on a small final statistic.
        Span<long> history = stackalloc long[indicator.Period];
        long s1 = 0, s2 = 0, s3 = 0, s4 = 0;
        int cursor = 0;
        for (int i = 0; i < seed.Length + end - start; i++)
        {
            if (cancellation.IsCancellationRequested) return;
            int row = start + i - seed.Length;
            double close = i < seed.Length ? seed[i] : output[1][row];
            long value = (long)Math.ScaleB(close, -grid);
            long expired = i >= indicator.Period ? history[cursor] : 0;
            history[cursor] = value;
            if (++cursor == indicator.Period) cursor = 0;
            unchecked
            {
                long square = value * value, oldSquare = expired * expired;
                s1 += value - expired;
                s2 += square - oldSquare;
                s3 += square * value - oldSquare * expired;
                if (indicator.Kurtosis) s4 += square * square - oldSquare * oldSquare;
            }
            if (i < seed.Length) continue;
            int n = Math.Min(i + 1, indicator.Period);
            output[0][row] = SampleShapeGridValue(n, s1, s2, s3, s4, indicator.Kurtosis, out double present);
            output[1][row] = present;
        }
    }

    private static double SampleShapeGridValue(int count, long sum, long squares, long cubes,
        long fourths, bool kurtosis, out double present)
    {
        present = 1;
        if (count < (kurtosis ? 4 : 3)) return 0;
        unchecked
        {
            Int128 n = count, s1 = sum;
            Int128 a = n * squares - s1 * s1;
            if (a == 0) { if (kurtosis) present = 0; return 0; }
            if (kurtosis)
            {
                Int128 c = n * n * n * fourths - 4 * n * n * s1 * cubes
                    + 6 * n * s1 * s1 * squares - 3 * s1 * s1 * s1 * s1;
                Int128 top = (n * n - 1) * c - 3 * (n - 1) * (n - 1) * a * a;
                Int128 bottom = a * a * (n - 2) * (n - 3);
                return JurikDyadic.IntegerRatio(top, bottom);
            }
            Int128 b = n * n * cubes - 3 * n * s1 * squares + 2 * s1 * s1 * s1;
            if (b == 0) return 0;
            UInt128 numerator = (UInt128)(b * b * n * (n - 1));
            UInt128 denominator = (UInt128)(a * a * a * (n - 2) * (n - 2));
            double root;
            if (denominator > ulong.MaxValue
                || !ExactPopulationDeviation.TryCompactScaledRoot(numerator, (ulong)denominator, 0, out root))
                root = ExactPopulationDeviation.ScaledRootRatio((BigInteger)numerator, (BigInteger)denominator, 0);
            return b < 0 ? -root : root;
        }
    }
}
#endif

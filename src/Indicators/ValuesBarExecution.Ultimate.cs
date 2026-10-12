#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static PointwiseRegion ComputeUltimateColumn(Bar[] source, double[][] output, OwnedBarBuffer? owned,
        int start, int end, WindowUltimateOscillator indicator, ReadOnlySpan<Bar> seed, ReadOnlySpan<Bar> tail,
        CancellationToken cancellation)
    {
        var region = new PointwiseRegion { InvalidOutputIndex = -1 };
        int count = seed.Length + end - start;
        var captured = System.Buffers.ArrayPool<Bar>.Shared.Rent(count);
        try
        {
            int grid = int.MaxValue;
            double maximum = 0;
            for (int i = 0; i < count; i++)
            {
                if (cancellation.IsCancellationRequested) return region;
                int row = start + i - seed.Length;
                var bar = i < seed.Length ? seed[i]
                    : row >= end - tail.Length ? tail[row - (end - tail.Length)] : source[row];
                if (!AllFieldsFinite(in bar)) { region.InvalidInput = true; region.Last = bar; return region; }
                captured[i] = bar;
                if (i >= seed.Length)
                {
                    region.Last = bar;
                    if (owned is not null) owned[row] = bar;
                }
                IncludeIntegerGrid(bar.High, ref grid, ref maximum);
                IncludeIntegerGrid(bar.Low, ref grid, ref maximum);
                IncludeIntegerGrid(bar.Close, ref grid, ref maximum);
            }
            if (grid == int.MaxValue) grid = 0;
            // Every pressure/range sum has magnitude <= 2^38. Therefore the
            // three-ratio numerator, including weights and 100, is bounded by
            // 700 * 2^114 < 2^124; the denominator is <= 7 * 2^114.
            long bound = (1L << 38) / (2L * indicator.LongPeriod);
            bool certified = Math.ScaleB(maximum, -grid) <= bound;
            if (!certified)
            {
                var state = new WindowUltimateOscillator.CompactState(
                    [indicator.ShortPeriod, indicator.MiddlePeriod, indicator.LongPeriod], indicator.ZeroRangeContribution, count);
                Span<double> values = stackalloc double[2];
                for (int i = 0; i < count; i++)
                {
                    if (cancellation.IsCancellationRequested) return region;
                    state.Update(in captured[i], values);
                    if (i < seed.Length) continue;
                    int row = start + i - seed.Length;
                    output[0][row] = values[0]; output[1][row] = values[1];
                    if (!double.IsFinite(values[0]) && region.InvalidOutputIndex < 0)
                    { region.InvalidOutputIndex = row; region.InvalidOutputValue = values[0]; }
                }
                return region;
            }
            // A 16-byte interval replaces each consumed 48-byte Bar in the
            // same rental. Its write cannot reach the next unread raw bar.
            var intervals = System.Runtime.InteropServices.MemoryMarshal.Cast<Bar, UltimateIntegerPair>(captured.AsSpan(0, count));
            long previousClose = 0;
            Span<int> periods = stackalloc int[] { indicator.ShortPeriod, indicator.MiddlePeriod, indicator.LongPeriod };
            Span<long> pressures = stackalloc long[3]; pressures.Clear();
            Span<long> ranges = stackalloc long[3]; ranges.Clear();
            for (int i = 0; i < count; i++)
            {
                if (cancellation.IsCancellationRequested) return region;
                var bar = captured[i];
                long high = (long)Math.ScaleB(bar.High, -grid);
                long low = (long)Math.ScaleB(bar.Low, -grid);
                long close = (long)Math.ScaleB(bar.Close, -grid);
                UltimateIntegerPair current = default;
                if (i > 0)
                {
                    current.Range = indicator.ZeroRangeContribution
                        ? Math.Max(high - low, Math.Max(Math.Abs(high - previousClose), Math.Abs(low - previousClose)))
                        : Math.Max(high, previousClose) - Math.Min(low, previousClose);
                    current.Pressure = close - Math.Min(low, previousClose);
                }
                previousClose = close;
                intervals[i] = current;
                if (i > 0)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        if (i > periods[k])
                        {
                            var expired = intervals[i - periods[k]];
                            pressures[k] -= expired.Pressure; ranges[k] -= expired.Range;
                        }
                        pressures[k] += current.Pressure; ranges[k] += current.Range;
                    }
                }
                if (i < seed.Length) continue;
                int row = start + i - seed.Length;
                bool defined = i >= indicator.LongPeriod;
                Int128 numerator = 0, denominator = 1;
                if (defined)
                for (int k = 0; k < 3; k++)
                {
                    if (ranges[k] == 0)
                    {
                        if (!indicator.ZeroRangeContribution) { defined = false; break; }
                        continue;
                    }
                    // Certified above: the complete scaled numerator fits 124 bits.
                    numerator = unchecked(numerator * ranges[k] + (Int128)(4 >> k) * pressures[k] * denominator);
                    denominator = unchecked(denominator * ranges[k]);
                }
                output[0][row] = defined ? JurikDyadic.FromInteger(unchecked(100 * numerator)).Ratio(JurikDyadic.FromInteger(unchecked(7 * denominator))) : 0;
                output[1][row] = defined ? 1 : 0;
            }
            return region;
        }
        finally { System.Buffers.ArrayPool<Bar>.Shared.Return(captured); }
    }

    private static void IncludeIntegerGrid(double value, ref int grid, ref double maximum)
    {
        maximum = Math.Max(maximum, Math.Abs(value));
        var factor = ExactMeanAccumulator.DecomposeFinite(value);
        if (factor.Integer != 0) grid = Math.Min(grid, factor.Exponent);
    }

    private struct UltimateIntegerPair
    {
        internal long Pressure;
        internal long Range;
    }
}
#endif

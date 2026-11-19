#if !NETFRAMEWORK
using System.Runtime.CompilerServices;
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
            double integerMaximum = Math.ScaleB(maximum, -grid);
            bool certified = integerMaximum <= bound;
            bool longWindows = certified || FitsUltimateLongWindows(integerMaximum, indicator.LongPeriod);
            if (!longWindows)
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
            bool narrow = certified && Core.UltimateRatioCertificate.FitsDouble(integerMaximum, indicator.LongPeriod);
            // A 16-byte interval replaces each consumed 48-byte Bar in the
            // same rental. Its write cannot reach the next unread raw bar.
            var intervals = System.Runtime.InteropServices.MemoryMarshal.Cast<Bar, UltimateIntegerPair>(captured.AsSpan(0, count));
            long previousClose = 0;
            Span<int> periods = stackalloc int[] { indicator.ShortPeriod, indicator.MiddlePeriod, indicator.LongPeriod };
            Span<long> pressures = stackalloc long[3]; pressures.Clear();
            Span<long> ranges = stackalloc long[3]; ranges.Clear();
            // Only uncertain publication needs dyadic scratch. Retain it after
            // the first fallback; certified integer rows never construct it.
            JurikDyadic[]? exactPressures = null, exactRanges = null;
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
                if (narrow)
                {
                    long numerator = 0, denominator = 1;
                    // The stronger worker certificate proves all of these
                    // products and sums fit 53 bits, including signed pressure.
                    unchecked
                    {
                        if (defined)
                        for (int k = 0; k < 3; k++)
                        {
                            if (ranges[k] == 0)
                            {
                                if (!indicator.ZeroRangeContribution) { defined = false; break; }
                                continue;
                            }
                            numerator = numerator * ranges[k] + (4L >> k) * pressures[k] * denominator;
                            denominator *= ranges[k];
                        }
                        output[0][row] = defined ? (double)(100 * numerator) / (7 * denominator) : 0;
                    }
                }
                else if (certified)
                {
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
                }
                else
                {
                    double value = 0;
                    if (defined && !TryUltimateLongCertificate(pressures, ranges, indicator.ZeroRangeContribution, out value))
                        defined = PublishUltimateLongWindow(pressures, ranges,
                            exactPressures ??= new JurikDyadic[3], exactRanges ??= new JurikDyadic[3],
                            indicator.ZeroRangeContribution, out value);
                    output[0][row] = value;
                }
                output[1][row] = defined ? 1 : 0;
            }
            return region;
        }
        finally { System.Buffers.ArrayPool<Bar>.Shared.Return(captured); }
    }

    private static bool FitsUltimateLongWindows(double integerMaximum, int longestPeriod)
    {
        // IncludeIntegerGrid makes the scaled maximum an exact integer. Check
        // the conversion range before casting, then compare with integer
        // arithmetic: a double-rounded threshold above 2^53 can round upward.
        if (!double.IsFinite(integerMaximum) || integerMaximum < 0
            || integerMaximum > (1L << 61) || longestPeriod < 1) return false;
        return 2 * (UInt128)(ulong)integerMaximum * (uint)longestPeriod <= ((UInt128)1 << 61);
    }

    private static bool TryUltimateLongCertificate(ReadOnlySpan<long> pressures, ReadOnlySpan<long> ranges,
        bool zero, out double result)
    {
        result = 0;
        // The existing worker guard bounds each signed sum by 2^61. The
        // shared price grid cancels, so these are the same 64-fractional-bit
        // enclosures as TryWeightedRatio, without dyadic normalization/copies.
        UInt128 lower = 0, upper = 0;
        for (int k = 0; k < 3; k++)
        {
            long pressure = pressures[k], range = ranges[k];
            if (range == 0)
            {
                if (!zero) return false;
                continue;
            }
            if (pressure < 0 || range < 0) return false;
            if (pressure == 0) continue;
            // Positive pressure << 64 fits within 126 magnitude bits.
            var divided = UInt128.DivRem((UInt128)pressure << 64, (UInt128)range);
            UInt128 ceiling = UltimateRatioCeiling(divided.Quotient, divided.Remainder);
            if (ceiling > (UInt128)1 << 64) return false;
            uint weight = (uint)(4 >> k);
            lower = unchecked(lower + divided.Quotient * weight);
            upper = unchecked(upper + ceiling * weight);
        }
        // Accepted bounds are <= 7*2^64; scaling by 100 stays below 2^74.
        Int128 denominator = (Int128)7 << 64;
        double low = JurikDyadic.IntegerRatio(unchecked((Int128)(lower * 100)), denominator);
        double high = JurikDyadic.IntegerRatio(unchecked((Int128)(upper * 100)), denominator);
        if (BitConverter.DoubleToInt64Bits(low) != BitConverter.DoubleToInt64Bits(high)) return false;
        result = low;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static UInt128 UltimateRatioCeiling(UInt128 quotient, UInt128 remainder) =>
        quotient + (remainder == 0 ? (UInt128)0 : 1);

    private static bool PublishUltimateLongWindow(ReadOnlySpan<long> pressures, ReadOnlySpan<long> ranges,
        Span<JurikDyadic> exactPressures, Span<JurikDyadic> exactRanges, bool zero, out double result)
    {
        // All six sums share the price grid, which cancels from every ratio.
        // 2*M*longestPeriod <= 2^61 bounds each signed sum and leaves the
        // certificate's 64 fractional bits within signed Int128.
        for (int k = 0; k < 3; k++)
        {
            exactPressures[k] = JurikDyadic.FromInteger(pressures[k]);
            exactRanges[k] = JurikDyadic.FromInteger(ranges[k]);
        }
        // Preserve CompactState's exact weighted expression for uncertain
        // rounding, signed pressure, inverted candles and zero denominators.
        var numerator = new JurikDyadic();
        var denominator = JurikDyadic.FromDouble(1);
        for (int k = 0; k < 3; k++)
        {
            if (exactRanges[k].Sign == 0)
            {
                if (!zero) { result = 0; return false; }
                continue;
            }
            numerator.Multiply(exactRanges[k]);
            var term = exactPressures[k];
            term.Multiply(4 >> k); term.Multiply(denominator);
            numerator.AddExact(term);
            denominator.Multiply(exactRanges[k]);
        }
        numerator.Multiply(100); denominator.Multiply(7);
        result = numerator.Ratio(denominator);
        return true;
    }

    internal static void IncludeIntegerGrid(double value, ref int grid, ref double maximum)
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

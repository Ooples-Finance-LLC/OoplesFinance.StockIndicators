#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static PointwiseRegion ComputePairColumn(Bar[] source, double[][] output, OwnedBarBuffer? owned,
        int start, int end, int period, ReadOnlySpan<Bar> seed, ReadOnlySpan<Bar> tail,
        CancellationToken cancellation, IIndicator indicator)
    {
        var region = new PointwiseRegion { InvalidOutputIndex = -1 };
        var correlation = indicator as WindowCorrelation;
        var pair = indicator as WindowPairStatistics;
        var left = correlation?.Left ?? pair!.Left;
        var right = correlation?.Right ?? pair!.Right;
        bool flatZero = correlation?.FlatZero ?? false;
        int slots = correlation is null ? 5 : 1;
        int count = seed.Length + end - start;
        var rented = System.Buffers.ArrayPool<double>.Shared.Rent(2 * count);
        try
        {
            var prices = rented.AsSpan(0, 2 * count);
            int grid = int.MaxValue;
            double maximum = 0;
            for (int i = 0; i < count; i++)
            {
                if (cancellation.IsCancellationRequested) return region;
                int row = start + i - seed.Length;
                var bar = i < seed.Length ? seed[i]
                    : row >= end - tail.Length ? tail[row - (end - tail.Length)] : source[row];
                if (!AllFieldsFinite(in bar)) { region.InvalidInput = true; region.Last = bar; return region; }
                if (i >= seed.Length)
                {
                    region.Last = bar;
                    if (owned is not null) owned[row] = bar;
                }
                double a = PairStatisticsWindow.Select(in bar, left), b = PairStatisticsWindow.Select(in bar, right);
                prices[2 * i] = a; prices[2 * i + 1] = b;
                maximum = Math.Max(maximum, Math.Max(Math.Abs(a), Math.Abs(b)));
                var da = ExactMeanAccumulator.DecomposeFinite(a);
                var db = ExactMeanAccumulator.DecomposeFinite(b);
                if (da.Integer != 0) grid = Math.Min(grid, da.Exponent);
                if (db.Integer != 0) grid = Math.Min(grid, db.Exponent);
            }
            if (grid == int.MaxValue) grid = 0;
            // The common grid is exact. Bounding P*|integer| by 2^30 proves
            // that sums, products and centered moments fit signed Int64.
            long bound = (1L << 30) / period;
            // Scaling is monotone. The largest magnitude certifies the whole
            // column without another pass over every captured price.
            bool certified = Math.ScaleB(maximum, -grid) <= bound;
            if (!certified)
            {
                // Replay captured values, never the mutable caller source.
                var state = new PairStatisticsWindow(period);
                for (int i = 0; i < count; i++)
                {
                    if (cancellation.IsCancellationRequested) return region;
                    state.Add(prices[2 * i], prices[2 * i + 1]);
                    if (i < seed.Length) continue;
                    int row = start + i - seed.Length;
                    // State still rotates its history. Reuse only a completed
                    // output row owned by this worker, never its neighbor's row.
                    if (i >= period && i > seed.Length
                        && prices[2 * i] == prices[2 * (i - period)] && prices[2 * i + 1] == prices[2 * (i - period) + 1]) // NOSONAR: Exact pair equality preserves all mathematical moments, including signed zeros.
                    {
                        CopyPairRow(output, slots, row);
                        continue;
                    }
                    for (int slot = 0; slot < slots; slot++)
                    {
                        var value = state.Read(slot, flatZero);
                        output[slot][row] = value ?? 0;
                        output[slot + slots][row] = value.HasValue ? 1 : 0;
                    }
                }
                return region;
            }
            // Normalize once in the captured buffer; every stored integer is
            // exactly representable, so eviction needs neither decoding nor a queue.
            double factor = grid >= -1023 ? Math.ScaleB(1d, -grid) : 0;
            for (int i = 0; i < prices.Length; i++)
            {
                if (cancellation.IsCancellationRequested) return region;
                // A finite power-of-two multiplier gives the same exact integer.
                // Subnormal source grids may require a factor beyond binary64.
                prices[i] = factor == 0 ? Math.ScaleB(prices[i], -grid) : prices[i] * factor;
            }
            long x = 0, y = 0, xx = 0, yy = 0, xy = 0;
            for (int i = 0; i < count; i++)
            {
                if (cancellation.IsCancellationRequested) return region;
                long a = unchecked((long)prices[2 * i]), b = unchecked((long)prices[2 * i + 1]);
                if (i >= period)
                {
                    long oldA = unchecked((long)prices[2 * (i - period)]), oldB = unchecked((long)prices[2 * (i - period) + 1]);
                    if (i > seed.Length && a == oldA && b == oldB)
                    {
                        // Captured history is indexed, so no queue needs rotation.
                        // Equal replacement leaves both moments and outputs intact.
                        CopyPairRow(output, slots, start + i - seed.Length);
                        continue;
                    }
                    x -= oldA; y -= oldB; xx -= oldA * oldA; yy -= oldB * oldB; xy -= oldA * oldB;
                }
                x += a; y += b; xx += a * a; yy += b * b; xy += a * b;
                if (i < seed.Length) continue;
                int row = start + i - seed.Length;
                long momentA = period * xx - x * x, momentB = period * yy - y * y, momentC = period * xy - x * y;
                for (int slot = 0; slot < slots; slot++)
                {
                    var value = i < period - 1 ? null
                        : PairStatisticsWindow.ReadSmall(momentA, momentB, momentC, period, grid, slot, flatZero);
                    output[slot][row] = value ?? 0;
                    output[slot + slots][row] = value.HasValue ? 1 : 0;
                }
            }
            return region;
        }
        finally { System.Buffers.ArrayPool<double>.Shared.Return(rented); }
    }

    private static void CopyPairRow(double[][] output, int slots, int row)
    {
        for (int slot = 0; slot < 2 * slots; slot++) output[slot][row] = output[slot][row - 1];
    }
}
#endif

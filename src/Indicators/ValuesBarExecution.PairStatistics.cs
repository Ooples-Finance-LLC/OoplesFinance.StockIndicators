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
                var da = ExactMeanAccumulator.DecomposeFinite(a);
                var db = ExactMeanAccumulator.DecomposeFinite(b);
                if (da.Integer != 0) grid = Math.Min(grid, da.Exponent);
                if (db.Integer != 0) grid = Math.Min(grid, db.Exponent);
            }
            if (grid == int.MaxValue) grid = 0;
            // The common grid is exact. Bounding P*|integer| by 2^30 proves
            // that sums, products and centered moments fit signed Int64.
            long bound = (1L << 30) / period;
            bool certified = true;
            foreach (double price in prices)
            {
                if (cancellation.IsCancellationRequested) return region;
                double scaled = Math.ScaleB(price, -grid);
                if (Math.Abs(scaled) > bound) { certified = false; break; }
            }
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
            for (int i = 0; i < prices.Length; i++)
            {
                if (cancellation.IsCancellationRequested) return region;
                prices[i] = Math.ScaleB(prices[i], -grid);
            }
            long x = 0, y = 0, xx = 0, yy = 0, xy = 0;
            for (int i = 0; i < count; i++)
            {
                if (cancellation.IsCancellationRequested) return region;
                if (i >= period)
                {
                    long oldA = (long)prices[2 * (i - period)], oldB = (long)prices[2 * (i - period) + 1];
                    x -= oldA; y -= oldB; xx -= oldA * oldA; yy -= oldB * oldB; xy -= oldA * oldB;
                }
                long a = (long)prices[2 * i], b = (long)prices[2 * i + 1];
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
}
#endif

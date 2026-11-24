#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    internal static bool MatchingLowGridEligible(IIndicator indicator, int count) =>
        count >= 8192 && indicator is MatchingLowCandle { Period: >= 1 and <= 64 };

    // MatchingLow only: reuse the existing worker ownership and validation
    // boundary, with the complete threshold window and anchor in each halo.
    private static PointwiseRegion ComputeMatchingLowGrid(Bar[] source, double[] output,
        OwnedBarBuffer? owned, int start, int end, ReadOnlySpan<Bar> seed,
        ReadOnlySpan<Bar> tail, CancellationToken cancellation, MatchingLowCandle indicator)
    {
        const int blockSize = 256;
        int halo = indicator.WarmupBars; // Configured period <=64, plus one anchor.
        Span<Bar> captured = stackalloc Bar[blockSize + 65];
        Span<long> storage = stackalloc long[3 * (blockSize + 66)];
        var region = new PointwiseRegion { InvalidOutputIndex = -1 };
        seed.CopyTo(captured);
        foreach (ref readonly var bar in seed)
            if (!AllFieldsFinite(in bar))
                return new PointwiseRegion { InvalidInput = true, Last = bar, InvalidOutputIndex = -1 };
        int prefix = seed.Length;
        int tailStart = end - tail.Length;
        for (int first = start; first < end;)
        {
            if (cancellation.IsCancellationRequested) return region;
            int count = Math.Min(blockSize, end - first);
            for (int j = 0; j < count; j++)
            {
                if (cancellation.IsCancellationRequested) return region;
                int index = first + j;
                var bar = index >= tailStart ? tail[index - tailStart] : source[index];
                region.Last = bar;
                if (!AllFieldsFinite(in bar)) { region.InvalidInput = true; return region; }
                captured[prefix + j] = bar;
                if (owned is not null) owned[index] = bar;
            }
            var block = captured[..(prefix + count)];
            var destination = output.AsSpan(first, count);
            if (!TryMatchingLowGridBlock(block, prefix, indicator, storage, destination, out _))
            {
                // No source reread or state translation: replay the captured
                // halo through the unchanged recognizer and exact arithmetic.
                var state = (IIndicatorState)indicator.CreateState()!;
                for (int j = 0; j < block.Length; j++)
                {
                    if (cancellation.IsCancellationRequested) return region;
                    double value = state.Update(in block[j]);
                    if (j >= prefix) destination[j - prefix] = value;
                }
            }
            first += count;
            prefix = Math.Min(halo, block.Length);
            block[^prefix..].CopyTo(captured);
        }
        return region;
    }

    // An accepted grid changes binary units exactly. Prices are bounded by
    // 2^60 and each prefix by Int64; period<=64 threshold products fit Int128.
    internal static bool TryMatchingLowGridBlock(ReadOnlySpan<Bar> bars, int prefix,
        MatchingLowCandle indicator, Span<long> storage, Span<double> output, out bool usedSinglePass)
    {
        usedSinglePass = false;
        int period = indicator.Period;
        if (period is < 1 or > 64) return false;
        int n = bars.Length;
        if (prefix < 0 || prefix > n || output.Length != n - prefix || storage.Length < 3 * n + 1) return false;
        var open = storage[..n];
        var close = storage.Slice(n, n);
        var range = storage.Slice(2 * n, n + 1);

        double anchor = n == 0 ? 0 : Math.Max(Math.Max(Math.Abs(bars[0].Open), Math.Abs(bars[0].Close)),
            Math.Max(Math.Abs(bars[0].High), Math.Abs(bars[0].Low)));
        int grid = anchor > 0 && double.IsFinite(anchor) ? Math.ILogB(anchor) - 56 : 0;
        if (TryMatchingLowGridPrefixes(bars, grid, true, open, close, range))
            usedSinglePass = true;
        else
        {
            // Preserve the previous exact resolver, then original-state replay,
            // when exponent spread or prefix bounds reject the first grid.
            grid = int.MaxValue;
            foreach (ref readonly var bar in bars)
            {
                if (!AllFieldsFinite(in bar)) return false;
                Include(bar.Open); Include(bar.High); Include(bar.Low); Include(bar.Close);
            }
            if (grid == int.MaxValue) grid = 0;
            if (!TryMatchingLowGridPrefixes(bars, grid, false, open, close, range)) return false;
        }
        int warmup = indicator.WarmupBars;
        for (int i = prefix; i < n; i++)
        {
            double value = 0;
            if (i >= warmup)
            {
                int b = i - 1;
                if (close[b] < open[b] && close[i] < open[i]
                    && (Int128)Math.Abs(close[i] - close[b]) * (period * 20)
                        <= (Int128)range[b] - range[b - period]) value = 100;
            }
            output[i - prefix] = value;
        }
        return true;

        void Include(double value)
        {
            var part = ExactMeanAccumulator.DecomposeFinite(value);
            if (part.Integer != 0) grid = Math.Min(grid, part.Exponent);
        }
    }

    private static bool TryMatchingLowGridPrefixes(ReadOnlySpan<Bar> bars, int grid, bool certify,
        Span<long> open, Span<long> close, Span<long> range)
    {
        double factor = grid >= -1023 ? Math.ScaleB(1, -grid) : double.PositiveInfinity;
        bool useFactor = double.IsFinite(factor) && factor > 0;
        range[0] = 0;
        const double limit = 1L << 60;
        for (int i = 0; i < bars.Length; i++)
        {
            ref readonly var bar = ref bars[i];
            if (!AllFieldsFinite(in bar)) return false;
            double o, h, l, v;
            if (useFactor) { o = bar.Open * factor; h = bar.High * factor; l = bar.Low * factor; v = bar.Close * factor; }
            else { o = Math.ScaleB(bar.Open, -grid); h = Math.ScaleB(bar.High, -grid); l = Math.ScaleB(bar.Low, -grid); v = Math.ScaleB(bar.Close, -grid); }
            if (!(Math.Abs(o) <= limit && Math.Abs(h) <= limit && Math.Abs(l) <= limit && Math.Abs(v) <= limit)) return false;
            long oi = (long)o, hi = (long)h, li = (long)l, ci = (long)v;
            if (certify && ((double)oi != o || (double)hi != h || (double)li != l || (double)ci != v)) return false; // NOSONAR: S1244 - A scaled price is on the grid only when it equals its own integer part exactly.
            // A power-of-two shift is exact for a nonzero integral result;
            // reject any smaller nonzero input that underflowed to scaled zero.
            if (certify && ((oi == 0 && bar.Open != 0) || (hi == 0 && bar.High != 0)
                || (li == 0 && bar.Low != 0) || (ci == 0 && bar.Close != 0))) return false;
            open[i] = oi; close[i] = ci;
            Int128 nextRange = (Int128)range[i] + hi - li;
            if (nextRange < long.MinValue || nextRange > long.MaxValue) return false;
            range[i + 1] = (long)nextRange;
        }
        return true;
    }
}
#endif

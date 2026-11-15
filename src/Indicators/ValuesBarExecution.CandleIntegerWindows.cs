#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    // Private batch prototype: retain the existing worker ownership and validation
    // boundary. A block includes the complete threshold window and anchor candle.
    private static PointwiseRegion ComputeCandleIntegerWindows(Bar[] source, double[] output,
        OwnedBarBuffer? owned, int start, int end, ReadOnlySpan<Bar> seed,
        ReadOnlySpan<Bar> tail, CancellationToken cancellation, IIndicator indicator)
    {
        const int blockSize = 256;
        int halo = indicator.WarmupBars; // caller limits both periods to 64
        Span<Bar> captured = stackalloc Bar[blockSize + 65];
        Span<long> storage = stackalloc long[5 * (blockSize + 66)];
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
            if (!TryCandleIntegerBlock(block, prefix, indicator, storage, destination))
            {
                // No source reread and no state translation: replay the captured
                // halo through exactly the original state and price arithmetic.
                var state = (IIndicatorState)((IndicatorBase)indicator).CreateState()!;
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

    internal static bool TryCandleIntegerBlock(ReadOnlySpan<Bar> bars, int prefix,
        IIndicator indicator, Span<long> storage, Span<double> output)
    {
        int bodyPeriod = indicator is OnNeckCandle neck ? neck.BodyPeriod : 0;
        int rangePeriod = indicator switch
        {
            MatchingLowCandle matching => matching.Period,
            OnNeckCandle onNeck => onNeck.EqualPeriod,
            _ => 0
        };
        if (rangePeriod is < 1 or > 64 || bodyPeriod > 64) return false;
        int n = bars.Length;
        if (prefix < 0 || prefix > n || output.Length != n - prefix || storage.Length < 5 * (n + 1))
            return false;
        int grid = int.MaxValue;
        foreach (ref readonly var bar in bars)
        {
            if (!AllFieldsFinite(in bar)) return false;
            Include(bar.Open); Include(bar.High); Include(bar.Low); Include(bar.Close);
        }
        if (grid == int.MaxValue) grid = 0;
        var open = storage[..n];
        var close = storage.Slice(n, n);
        var low = storage.Slice(2 * n, n);
        var body = storage.Slice(3 * n, n + 1);
        var range = storage.Slice(4 * n + 1, n + 1);
        body[0] = range[0] = 0;
        // Every normalized price is exact and bounded by 2^60. Differences
        // fit Int64; checked prefixes fit Int64. Window subtraction and all
        // threshold products then fit Int128 (< 2^74, periods <= 64).
        const double priceLimit = 1L << 60;
        for (int i = 0; i < n; i++)
        {
            var bar = bars[i];
            double o = Math.ScaleB(bar.Open, -grid), h = Math.ScaleB(bar.High, -grid);
            double l = Math.ScaleB(bar.Low, -grid), c = Math.ScaleB(bar.Close, -grid);
            if (!(Math.Abs(o) <= priceLimit && Math.Abs(h) <= priceLimit
                && Math.Abs(l) <= priceLimit && Math.Abs(c) <= priceLimit)) return false;
            open[i] = (long)o; close[i] = (long)c; low[i] = (long)l;
            Int128 nextBody = (Int128)body[i] + Math.Abs(close[i] - open[i]);
            Int128 nextRange = (Int128)range[i] + (long)h - low[i];
            if (nextBody > long.MaxValue || nextRange < long.MinValue || nextRange > long.MaxValue) return false;
            body[i + 1] = (long)nextBody;
            range[i + 1] = (long)nextRange;
        }
        int warmup = Math.Max(bodyPeriod, rangePeriod) + 1;
        for (int i = prefix; i < n; i++)
        {
            double value = 0;
            if (i >= warmup)
            {
                Int128 rangeSum = (Int128)range[i - 1] - range[i - rangePeriod - 1];
                if (bodyPeriod == 0)
                {
                    if (close[i - 1] < open[i - 1] && close[i] < open[i]
                        && (Int128)Math.Abs(close[i] - close[i - 1]) * (rangePeriod * 20) <= rangeSum)
                        value = 100;
                }
                else if (close[i - 1] < open[i - 1] && close[i] >= open[i] && open[i] < low[i - 1]
                    && (Int128)(open[i - 1] - close[i - 1]) * bodyPeriod
                        > (Int128)body[i - 1] - body[i - bodyPeriod - 1]
                    && (Int128)Math.Abs(close[i] - low[i - 1]) * (rangePeriod * 20) <= rangeSum)
                    value = -100;
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
}
#endif

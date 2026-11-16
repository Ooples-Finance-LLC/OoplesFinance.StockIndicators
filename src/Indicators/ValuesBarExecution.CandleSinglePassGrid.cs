#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private enum CandleGridKind { MatchingLow, SeparatingLines, TasukiGap }
    private readonly record struct CandleGridConfiguration(CandleGridKind Kind, int Range, int Body, int Equal);

    private static bool IsCandleGridKernel<T>() => typeof(T) == typeof(SequenceKernel)
        || typeof(T) == typeof(MatchedLinesKernel) || typeof(T) == typeof(GapContinuationKernel);

    private static bool TryCandleSinglePassConfiguration(IIndicator indicator, out CandleGridConfiguration c)
    {
        c = indicator switch
        {
            MatchingLowCandle x => new(CandleGridKind.MatchingLow, x.Period, 0, 0),
            SeparatingLinesCandle x => new(CandleGridKind.SeparatingLines, x.ShadowPeriod, x.BodyPeriod, x.EqualPeriod),
            TasukiGapCandle x => new(CandleGridKind.TasukiGap, x.Period, 0, 0),
            _ => default,
        };
        return c.Range is >= 1 and <= 64 && c.Body is >= 0 and <= 64 && c.Equal is >= 0 and <= 64;
    }

    internal static bool CandleSinglePassGridEligible(IIndicator indicator, int count) =>
        count >= 8192 && TryCandleSinglePassConfiguration(indicator, out _);

    // Private batch prototype: retain the existing worker ownership and validation
    // boundary. A block includes the complete threshold window and anchor candle.
    private static PointwiseRegion ComputeCandleSinglePassGrid(Bar[] source, double[] output,
        OwnedBarBuffer? owned, int start, int end, ReadOnlySpan<Bar> seed,
        ReadOnlySpan<Bar> tail, CancellationToken cancellation, IIndicator indicator)
    {
        const int blockSize = 256;
        int halo = indicator.WarmupBars; // periods <=64, plus at most three anchors
        Span<Bar> captured = stackalloc Bar[blockSize + 67];
        Span<long> storage = stackalloc long[5 * (blockSize + 68)];
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
            if (!TryCandleSinglePassGridBlock(block, prefix, indicator, storage, destination, out _))
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

    // Accepted conversion is an exact change of binary units. All predicates below
    // are homogeneous integer comparisons; no rounded mean or price is introduced.
    // Prices are bounded by 2^60 and prefixes by Int64. With periods at most 64,
    // window differences and threshold products remain below 2^74 in Int128.
    internal static bool TryCandleSinglePassGridBlock(ReadOnlySpan<Bar> bars, int prefix,
        IIndicator indicator, Span<long> storage, Span<double> output, out bool usedSinglePass)
    {
        usedSinglePass = false;
        if (!TryCandleSinglePassConfiguration(indicator, out var c)) return false;
        int n = bars.Length;
        if (prefix < 0 || prefix > n || output.Length != n - prefix || storage.Length < 5 * (n + 1)) return false;
        var open = storage[..n];
        var close = storage.Slice(n, n);
        var low = storage.Slice(2 * n, n);
        var body = storage.Slice(3 * n, n + 1);
        var range = storage.Slice(4 * n + 1, n + 1);
        bool needsBody = c.Kind == CandleGridKind.SeparatingLines;

        double anchor = n == 0 ? 0 : Math.Max(Math.Max(Math.Abs(bars[0].Open), Math.Abs(bars[0].Close)),
            Math.Max(Math.Abs(bars[0].High), Math.Abs(bars[0].Low)));
        int grid = anchor > 0 && double.IsFinite(anchor) ? Math.ILogB(anchor) - 56 : 0;
        if (TryCandleGridPrefixes(bars, grid, true, needsBody, open, close, low, body, range))
            usedSinglePass = true;
        else
        {
            // Keep the previous exact resolver, then the original state replay,
            // for grids whose exponent spread or prefix bounds need more room.
            grid = int.MaxValue;
            foreach (ref readonly var bar in bars)
            {
                if (!AllFieldsFinite(in bar)) return false;
                Include(bar.Open); Include(bar.High); Include(bar.Low); Include(bar.Close);
            }
            if (grid == int.MaxValue) grid = 0;
            if (!TryCandleGridPrefixes(bars, grid, false, needsBody, open, close, low, body, range)) return false;
        }
        int warmup = indicator.WarmupBars;
        for (int i = prefix; i < n; i++)
        {
            double value = 0;
            if (i >= warmup)
            {
                int b = i - 1;
                if (c.Kind == CandleGridKind.MatchingLow)
                {
                    if (close[b] < open[b] && close[i] < open[i]
                        && (Int128)Math.Abs(close[i] - close[b]) * (c.Range * 20)
                            <= (Int128)range[b] - range[b - c.Range]) value = 100;
                }
                else if (c.Kind == CandleGridKind.SeparatingLines)
                {
                    if ((close[b] < open[b]) != (close[i] < open[i])
                        && (Int128)Math.Abs(close[i] - open[i]) * c.Body > (Int128)body[i] - body[i - c.Body]
                        && (Int128)Math.Abs(open[i] - open[b]) * (c.Equal * 20) <= (Int128)range[b] - range[b - c.Equal])
                    {
                        long shadow = close[i] >= open[i] ? open[i] - low[i]
                            : low[i] + (range[i + 1] - range[i]) - open[i];
                        if ((Int128)range[i] - range[i - c.Range] > (Int128)shadow * (c.Range * 10))
                            value = close[i] >= open[i] ? 100 : -100;
                    }
                }
                else
                {
                    int a = i - 2;
                    bool white = close[b] >= open[b];
                    if (white != (close[i] >= open[i]) && open[i] > Math.Min(open[b], close[b])
                        && open[i] < Math.Max(open[b], close[b]))
                    {
                        bool gap = white ? Math.Min(open[b], close[b]) > Math.Max(open[a], close[a])
                            : Math.Max(open[b], close[b]) < Math.Min(open[a], close[a]);
                        bool retreat = white ? close[i] < open[b] && close[i] > Math.Max(open[a], close[a])
                            : close[i] > open[b] && close[i] < Math.Min(open[a], close[a]);
                        if (gap && retreat)
                        {
                            long difference = Math.Abs(close[b] - open[b]) - Math.Abs(close[i] - open[i]);
                            if ((Int128)Math.Abs(difference) * (c.Range * 5) < (Int128)range[b] - range[b - c.Range])
                                value = white ? 100 : -100;
                        }
                    }
                }
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

    private static bool TryCandleGridPrefixes(ReadOnlySpan<Bar> bars, int grid, bool certify,
        bool needsBody, Span<long> open, Span<long> close, Span<long> low, Span<long> body, Span<long> range)
    {
        double factor = grid >= -1023 ? Math.ScaleB(1, -grid) : double.PositiveInfinity;
        bool useFactor = double.IsFinite(factor) && factor > 0;
        body[0] = range[0] = 0;
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
            if (certify && ((double)oi != o || (double)hi != h || (double)li != l || (double)ci != v)) return false;
            // A power-of-two shift is exact for a nonzero integral result, but
            // an extremely smaller source price could have underflowed to zero.
            if (certify && ((oi == 0 && bar.Open != 0) || (hi == 0 && bar.High != 0)
                || (li == 0 && bar.Low != 0) || (ci == 0 && bar.Close != 0))) return false;
            open[i] = oi; close[i] = ci;
            if (needsBody) low[i] = li;
            Int128 nextRange = (Int128)range[i] + hi - li;
            if (nextRange < long.MinValue || nextRange > long.MaxValue) return false;
            range[i + 1] = (long)nextRange;
            if (needsBody)
            {
                Int128 nextBody = (Int128)body[i] + Math.Abs(ci - oi);
                if (nextBody > long.MaxValue) return false;
                body[i + 1] = (long)nextBody;
            }
        }
        return true;
    }
}
#endif

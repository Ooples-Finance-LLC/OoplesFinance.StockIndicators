#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private enum CertifiedCandleBlockKind { MatHold, Breakaway, ConcealingBabySwallow, LadderBottom, ThreeStarsInSouth }

    private interface ICertifiedCandleBlockState<T> where T : struct, ICertifiedCandleBlockState<T>
    {
        static abstract T Create(IIndicator indicator);
        double Update(in Bar bar);
        void Reset();
    }

    private readonly struct FiveContinuationBlockState(FiveCandleContinuationState state) : ICertifiedCandleBlockState<FiveContinuationBlockState>
    {
        public static FiveContinuationBlockState Create(IIndicator indicator) => new((FiveCandleContinuationState)((IndicatorBase)indicator).CreateState()!);
        public double Update(in Bar bar) => state.Update(in bar);
        public void Reset() => state.Reset();
    }

    private readonly struct ExtendedReversalBlockState(ExtendedReversalState state) : ICertifiedCandleBlockState<ExtendedReversalBlockState>
    {
        public static ExtendedReversalBlockState Create(IIndicator indicator) => new((ExtendedReversalState)((IndicatorBase)indicator).CreateState()!);
        public double Update(in Bar bar) => state.Update(in bar);
        public void Reset() => state.Reset();
    }

    private readonly struct SouthBlockState(ExhaustionCandleState state) : ICertifiedCandleBlockState<SouthBlockState>
    {
        public static SouthBlockState Create(IIndicator indicator) => new((ExhaustionCandleState)((IndicatorBase)indicator).CreateState()!);
        public double Update(in Bar bar) => state.Update(in bar);
        public void Reset() => state.Reset();
    }

    // Geometry can prove zero without updating rolling threshold state. Restore
    // that unchanged state from captured history before the next possible block.
    private static PointwiseRegion ComputeCertifiedCandleBlocks<TState>(Bar[] source, double[] output, OwnedBarBuffer? owned,
        int start, int end, int window, ReadOnlySpan<Bar> seed, ReadOnlySpan<Bar> tail,
        CancellationToken cancellation, IIndicator indicator, CertifiedCandleBlockKind kind)
        where TState : struct, ICertifiedCandleBlockState<TState>
    {
        const int blockSize = 32;
        // Dispatch supplies WarmupBars + 1. The guards cap the preceding halo
        // at 68 bars; a whole block and that halo fit in this fixed stack span.
        int lookback = window - 1;
        Span<Bar> captured = stackalloc Bar[68 + blockSize];
        var region = new PointwiseRegion { InvalidOutputIndex = -1 };
        int history = seed.Length;
        for (int i = 0; i < history; i++)
        {
            if (cancellation.IsCancellationRequested) return region;
            ref readonly var bar = ref seed[i];
            if (!AllFieldsFinite(in bar))
                return new PointwiseRegion { InvalidInput = true, Last = bar, InvalidOutputIndex = -1 };
            captured[i] = bar;
        }

        TState state = default;
        bool created = false, dirty = true;
        int consecutive = 0;
        int tailStart = end - tail.Length;
        for (int position = start; position < end;)
        {
            int count = Math.Min(blockSize, end - position);
            for (int offset = 0; offset < count; offset++)
            {
                if (cancellation.IsCancellationRequested) return region;
                int row = position + offset;
                var bar = row >= tailStart ? tail[row - tailStart] : source[row];
                region.Last = bar;
                if (!AllFieldsFinite(in bar)) { region.InvalidInput = true; return region; }
                captured[history + offset] = bar;
                if (owned is not null) owned[row] = bar;
            }

            bool possible = false;
            int first = Math.Max(0, indicator.WarmupBars - position);
            for (int offset = first; offset < count; offset++)
            {
                if (cancellation.IsCancellationRequested) return region;
                if (CertifiedCandleBlockGeometry(captured, history + offset, kind))
                {
                    possible = true;
                    break;
                }
            }

            if (!possible)
            {
                output.AsSpan(position, count).Clear();
                consecutive = 0;
                dirty = true;
            }
            else
            {
                if (!created) { state = TState.Create(indicator); created = true; }
                if (dirty)
                {
                    state.Reset();
                    for (int i = 0; i < history; i++)
                    {
                        if (cancellation.IsCancellationRequested) return region;
                        state.Update(in captured[i]);
                    }
                    dirty = false;
                }
                for (int offset = 0; offset < count; offset++)
                {
                    if (cancellation.IsCancellationRequested) return region;
                    output[position + offset] = state.Update(in captured[history + offset]);
                }

                // Bound prefilter/capture overhead on dense input, then continue
                // the same original state for the rest of this worker's region.
                if (++consecutive == 4)
                {
                    for (int row = position + count; row < end; row++)
                    {
                        if (cancellation.IsCancellationRequested) return region;
                        var bar = row >= tailStart ? tail[row - tailStart] : source[row];
                        region.Last = bar;
                        if (!AllFieldsFinite(in bar)) { region.InvalidInput = true; return region; }
                        output[row] = state.Update(in bar);
                        if (owned is not null) owned[row] = bar;
                    }
                    return region;
                }
            }

            int retained = Math.Min(lookback, history + count);
            captured.Slice(history + count - retained, retained).CopyTo(captured);
            history = retained;
            position += count;
        }
        return region;
    }

    // Only necessary comparisons copied from the original Signal methods.
    // All rolling thresholds and MatHold penetration retain their exact state.
    private static bool CertifiedCandleBlockGeometry(ReadOnlySpan<Bar> bars, int index, CertifiedCandleBlockKind kind)
    {
        if (kind == CertifiedCandleBlockKind.ThreeStarsInSouth)
            return ThreeStarsInSouthBlockGeometry(in bars[index - 2], in bars[index - 1], in bars[index]);
        ref readonly var a = ref bars[index - 4];
        ref readonly var b = ref bars[index - 3];
        ref readonly var c = ref bars[index - 2];
        ref readonly var d = ref bars[index - 1];
        ref readonly var e = ref bars[index];
        return kind switch
        {
            CertifiedCandleBlockKind.MatHold => a.Close >= a.Open
                && b.Close < b.Open && e.Close >= e.Open && b.Close > a.Close
                && Math.Min(c.Open, c.Close) < a.Close && Math.Min(d.Open, d.Close) < a.Close
                && Math.Max(c.Open, c.Close) < b.Open
                && Math.Max(d.Open, d.Close) < Math.Max(c.Open, c.Close)
                && e.Open > d.Close && e.Close > Math.Max(Math.Max(b.High, c.High), d.High),
            CertifiedCandleBlockKind.ConcealingBabySwallow => b.Close < b.Open
                && c.Close < c.Open && d.Close < d.Open && e.Close < e.Open
                && d.Open < c.Close && d.High > c.Close && e.High > d.High && e.Low < d.Low,
            CertifiedCandleBlockKind.LadderBottom => a.Close < a.Open
                && b.Close < b.Open && c.Close < c.Open && d.Close < d.Open
                && a.Open > b.Open && b.Open > c.Open && a.Close > b.Close && b.Close > c.Close
                && e.Close >= e.Open && e.Open > d.Open && e.Close > d.High,
            CertifiedCandleBlockKind.Breakaway => (a.Close >= a.Open) == (b.Close >= b.Open)
                && (a.Close >= a.Open) == (d.Close >= d.Open)
                && (a.Close >= a.Open) != (e.Close >= e.Open)
                && (a.Close >= a.Open
                    ? b.Open > a.Close && c.High > b.High && c.Low > b.Low && d.High > c.High && d.Low > c.Low && e.Close < b.Open && e.Close > a.Close
                    : b.Open < a.Close && c.High < b.High && c.Low < b.Low && d.High < c.High && d.Low < c.Low && e.Close > b.Open && e.Close < a.Close),
            _ => false
        };
    }
}
#endif

#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    // Geometry can prove zero without updating rolling threshold state. Restore
    // that unchanged state from captured history before the next possible block.
    private static PointwiseRegion ComputeBreakawayBlocks(Bar[] source, double[] output, OwnedBarBuffer? owned,
        int start, int end, int window, ReadOnlySpan<Bar> seed, ReadOnlySpan<Bar> tail,
        CancellationToken cancellation, BreakawayCandle indicator)
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

        ExtendedReversalState? state = null;
        bool dirty = true;
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
                if (BreakawayBlockGeometry(in captured[history + offset - 4], in captured[history + offset - 3],
                    in captured[history + offset - 2], in captured[history + offset - 1], in captured[history + offset]))
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
                state ??= (ExtendedReversalState)indicator.CreateState()!;
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

    // Verbatim necessary price conditions from ExtendedReversalState.Signal.
    // The original state alone evaluates the long-body threshold and publishes.
    private static bool BreakawayBlockGeometry(in Bar a, in Bar b, in Bar c, in Bar d, in Bar last)
    {
        bool white = a.Close >= a.Open;
        if (white != (b.Close >= b.Open) || white != (d.Close >= d.Open)
            || white == (last.Close >= last.Open)) return false;
        return white
            ? b.Open > a.Close
                && c.High > b.High && c.Low > b.Low
                && d.High > c.High && d.Low > c.Low
                && last.Close < b.Open && last.Close > a.Close
            : b.Open < a.Close
                && c.High < b.High && c.Low < b.Low
                && d.High < c.High && d.Low < c.Low
                && last.Close > b.Open && last.Close < a.Close;
    }
}
#endif

#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    // These are necessary raw comparisons from the original recognizer, not
    // approximations to its exact body, length or rolling threshold arithmetic.
    private static bool ThreeStarsHasStructure(in Bar a, in Bar b, in Bar c) =>
        a.Close < a.Open && b.Close < b.Open && c.Close < c.Open
        && b.Open > a.Close && b.Open <= a.High
        && b.Low < a.Close && b.Low >= a.Low
        && c.Low > b.Low && c.High < b.High;

    private static PointwiseRegion ComputeThreeStarsBlocks(Bar[] source, double[] output, OwnedBarBuffer? owned,
        int start, int end, ReadOnlySpan<Bar> seed, ReadOnlySpan<Bar> tail,
        CancellationToken cancellation, ThreeStarsInSouthCandle indicator)
    {
        const int blockSize = 64;
        int warmup = indicator.WarmupBars; // Guarded periods <=64: at most66 halo bars.
        Span<Bar> captured = stackalloc Bar[66 + blockSize];
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

        ExhaustionCandleState? state = null;
        bool dirty = true;
        int consecutive = 0;
        int tailStart = end - tail.Length;
        for (int position = start; position < end;)
        {
            int count = Math.Min(blockSize, end - position);
            // Read each source bar once, including the previously captured tail.
            // Zero blocks retain the same validation and owned-history writes.
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
            int first = Math.Max(0, warmup - position);
            for (int offset = first; offset < count; offset++)
            {
                if (cancellation.IsCancellationRequested) return region;
                int index = history + offset;
                if (ThreeStarsHasStructure(in captured[index - 2], in captured[index - 1], in captured[index]))
                {
                    possible = true;
                    break;
                }
            }

            if (!possible)
            {
                output.AsSpan(position, count).Clear();
                dirty = true;
                consecutive = 0;
            }
            else
            {
                state ??= (ExhaustionCandleState)indicator.CreateState()!;
                if (dirty)
                {
                    state.Reset();
                    // The complete warmup halo restores both anchors, every
                    // exact window and its two lagged copies before publication.
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

                // Bound scanning overhead for dense input; this state ends at
                // exactly the preceding block boundary and needs no translation.
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

            int retained = Math.Min(warmup, history + count);
            captured.Slice(history + count - retained, retained).CopyTo(captured);
            history = retained;
            position += count;
        }
        return region;
    }
}
#endif

#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    // A failed necessary price predicate proves zero without evaluating a
    // threshold. Restore the original bounded state before a possible match.
    private static PointwiseRegion ComputeAbandonedBlocks(Bar[] source, double[] output, OwnedBarBuffer? owned,
        int start, int end, int lookback, ReadOnlySpan<Bar> seed, ReadOnlySpan<Bar> tail,
        CancellationToken cancellation, AbandonedBabyCandle indicator)
    {
        const int blockSize = 64;
        Span<Bar> captured = stackalloc Bar[67 + blockSize];
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

        StarReversalState? state = null;
        bool dirty = true;
        int consecutive = 0;
        int tailStart = end - tail.Length;
        for (int position = start; position < end;)
        {
            int count = Math.Min(blockSize, end - position);
            // Use the existing captured tail and read each other source bar once.
            // Both predicates and numerical replay consume these same values.
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
                int index = history + offset;
                if (StarReversalState.HasStructure(in captured[index - 2], in captured[index - 1],
                    in captured[index], null, true))
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
                state ??= (StarReversalState)indicator.CreateState()!;
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

                // Dense input pays only a bounded scan/capture startup, then
                // continues the unchanged state without blocks or extra copies.
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
}
#endif

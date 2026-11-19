#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static bool UsesGapSideReplay(int count, IIndicator indicator) => count >= 8192
        && indicator is GapSideBySideWhiteLinesCandle { NearPeriod: <= 64, EqualPeriod: <= 64 };

    // The intervals only disprove necessary conditions. Possible matches always
    // run through the unchanged original state, including all threshold math.
    private static PointwiseRegion ComputeGapSideReplay(Bar[] source, double[] output, OwnedBarBuffer? owned,
        int start, int end, int window, ReadOnlySpan<Bar> seed, ReadOnlySpan<Bar> tail,
        CancellationToken cancellation, IIndicator indicator)
    {
        const int blockSize = 32;
        int lookback = window - 1;
        Span<Bar> captured = stackalloc Bar[66 + blockSize];
        Span<double> ranges = stackalloc double[66 + blockSize];
        var region = new PointwiseRegion { InvalidOutputIndex = -1 };
        int history = seed.Length;
        for (int i = 0; i < history; i++)
        {
            if (cancellation.IsCancellationRequested) return region;
            ref readonly var bar = ref seed[i];
            if (!AllFieldsFinite(in bar))
                return new PointwiseRegion { InvalidInput = true, Last = bar, InvalidOutputIndex = -1 };
            captured[i] = bar;
            ranges[i] = GapSideUpperDifference(bar.High, bar.Low);
        }

        IIndicatorState? state = null;
        int lastConsumed = -1;
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
                ranges[history + offset] = GapSideUpperDifference(bar.High, bar.Low);
                if (owned is not null) owned[row] = bar;
            }

            // Every contributing range window is contained in this halo/block.
            // A maximum also bounds signed means when OHLC is unordered.
            double maximum = double.NegativeInfinity;
            for (int i = 0; i < history + count; i++) maximum = Math.Max(maximum, ranges[i]);
            double near = GapSideUpperThreshold(maximum, 5);
            double equal = GapSideUpperThreshold(maximum, 20);
            output.AsSpan(position, count).Clear();
            int first = Math.Max(0, indicator.WarmupBars - position);
            for (int offset = first; offset < count; offset++)
            {
                if (cancellation.IsCancellationRequested) return region;
                int row = position + offset;
                int index = history + offset;
                if (double.IsFinite(maximum) && !GapSidePossible(in captured[index - 2],
                    in captured[index - 1], in captured[index], near, equal)) continue;

                // Merge adjacent replay support rather than processing entire
                // blocks containing a hit. Queues and exact sums are reused.
                if (state is null || row - lastConsumed > lookback)
                {
                    state ??= (IIndicatorState)((IndicatorBase)indicator).CreateState()!;
                    state.Reset();
                    lastConsumed = row - lookback - 1;
                }
                for (int replay = lastConsumed + 1; replay <= row; replay++)
                {
                    if (cancellation.IsCancellationRequested) return region;
                    double value = state.Update(in captured[history + replay - position]);
                    if (replay == row)
                    {
                        output[row] = value;
                        if (!double.IsFinite(value) && region.InvalidOutputIndex < 0)
                        {
                            region.InvalidOutputIndex = row;
                            region.InvalidOutputValue = value;
                        }
                    }
                }
                lastConsumed = row;
            }

            int retained = Math.Min(lookback, history + count);
            captured.Slice(history + count - retained, retained).CopyTo(captured);
            ranges.Slice(history + count - retained, retained).CopyTo(ranges);
            history = retained;
            position += count;
        }
        return region;
    }

    private static double GapSideLowerDifference(double left, double right)
    {
        double value = left - right;
        return double.IsFinite(value) ? Math.BitDecrement(value) : double.NegativeInfinity;
    }

    private static double GapSideUpperDifference(double left, double right)
    {
        double value = left - right;
        return double.IsFinite(value) ? Math.BitIncrement(value) : double.PositiveInfinity;
    }

    private static double GapSideAbsoluteLowerDifference(double left, double right) =>
        Math.Max(0, GapSideLowerDifference(Math.Max(left, right), Math.Min(left, right)));

    private static double GapSideUpperThreshold(double maximum, double factor) => Math.BitIncrement(maximum / factor);

    private static bool GapSidePossible(in Bar a, in Bar b, in Bar c, double near, double equal)
    {
        // White includes a doji. Only the original state may certify a signal.
        if (b.Close < b.Open || c.Close < c.Open) return false;
        bool up = b.Open > Math.Max(a.Open, a.Close) && c.Open > Math.Max(a.Open, a.Close);
        bool down = b.Close < Math.Min(a.Open, a.Close) && c.Close < Math.Min(a.Open, a.Close);
        if (!up && !down) return false;

        double bLower = GapSideLowerDifference(b.Close, b.Open);
        double bUpper = GapSideUpperDifference(b.Close, b.Open);
        double cLower = GapSideLowerDifference(c.Close, c.Open);
        double cUpper = GapSideUpperDifference(c.Close, c.Open);
        double lower = GapSideLowerDifference(cLower, bUpper);
        double upper = GapSideUpperDifference(cUpper, bLower);
        if (!double.IsFinite(bLower) || !double.IsFinite(bUpper)
            || !double.IsFinite(cLower) || !double.IsFinite(cUpper)
            || !double.IsFinite(lower) || !double.IsFinite(upper)) return true;

        double bodyLower = Math.Max(0, Math.Max(lower, -upper));
        return GapSideAbsoluteLowerDifference(c.Open, b.Open) <= equal && bodyLower <= near;
    }
}
#endif

#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static bool UsesCandleTopologyReplay(IIndicator indicator) => indicator is
        StalledCandle { LongPeriod: <= 64, ShortPeriod: <= 64, ShadowPeriod: <= 64, NearPeriod: <= 64 }
        or IdenticalThreeCrowsCandle { ShadowPeriod: <= 64, EqualPeriod: <= 64 }
        or ThreeStarsInSouthCandle { LongPeriod: <= 64, ShortPeriod: <= 64, ShadowPeriod: <= 64 };

    // The intervals only disprove necessary conditions. Possible matches always
    // run through the unchanged original state, including all threshold math.
    private static PointwiseRegion ComputeCandleTopologyReplay(Bar[] source, double[] output, OwnedBarBuffer? owned,
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
            ranges[i] = TopologyUpperDifference(bar.High, bar.Low);
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
                ranges[history + offset] = TopologyUpperDifference(bar.High, bar.Low);
                if (owned is not null) owned[row] = bar;
            }

            // Every contributing range window is contained in this halo/block.
            // A maximum also bounds signed means when OHLC is unordered.
            double maximum = double.NegativeInfinity;
            for (int i = 0; i < history + count; i++) maximum = Math.Max(maximum, ranges[i]);
            double shadow = TopologyUpperThreshold(maximum, 10);
            double near = TopologyUpperThreshold(maximum, 5);
            double equal = TopologyUpperThreshold(maximum, 20);
            output.AsSpan(position, count).Clear();
            int first = Math.Max(0, indicator.WarmupBars - position);
            for (int offset = first; offset < count; offset++)
            {
                if (cancellation.IsCancellationRequested) return region;
                int row = position + offset;
                int index = history + offset;
                if (double.IsFinite(maximum) && !CandleTopologyPossible(in captured[index - 2],
                    in captured[index - 1], in captured[index], indicator, shadow, near, equal)) continue;

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

    private static double TopologyLowerDifference(double left, double right)
    {
        double value = left - right;
        return double.IsFinite(value) ? Math.BitDecrement(value) : double.NegativeInfinity;
    }

    private static double TopologyUpperDifference(double left, double right)
    {
        double value = left - right;
        return double.IsFinite(value) ? Math.BitIncrement(value) : double.PositiveInfinity;
    }

    private static double TopologyAbsoluteLowerDifference(double left, double right) =>
        Math.Max(0, TopologyLowerDifference(Math.Max(left, right), Math.Min(left, right)));

    private static double TopologyUpperThreshold(double maximum, double factor) => Math.BitIncrement(maximum / factor);

    private static bool CandleTopologyPossible(in Bar a, in Bar b, in Bar c, IIndicator indicator,
        double shadow, double near, double equal)
    {
        if (indicator is StalledCandle)
            return a.Close >= a.Open && b.Close >= b.Open && c.Close >= c.Open
                && c.Close > b.Close && b.Close > a.Close && b.Open > a.Open
                && TopologyLowerDifference(b.High, b.Close) < shadow
                && TopologyLowerDifference(b.Open, a.Close) <= near
                && TopologyLowerDifference(b.Close, c.Close) <= near;
        if (indicator is IdenticalThreeCrowsCandle)
            return a.Close < a.Open && b.Close < b.Open && c.Close < c.Open
                && c.Close < b.Close && b.Close < a.Close
                && TopologyLowerDifference(a.Close, a.Low) < shadow
                && TopologyLowerDifference(b.Close, b.Low) < shadow
                && TopologyLowerDifference(c.Close, c.Low) < shadow
                && TopologyAbsoluteLowerDifference(b.Open, a.Close) <= equal
                && TopologyAbsoluteLowerDifference(c.Open, b.Close) <= equal;
        return a.Close < a.Open && b.Close < b.Open && c.Close < c.Open
            && b.Open > a.Close && b.Open <= a.High
            && b.Low < a.Close && b.Low >= a.Low
            && c.Low > b.Low && c.High < b.High
            && TopologyLowerDifference(c.Close, c.Low) < shadow
            && TopologyLowerDifference(c.High, c.Open) < shadow
            && TopologyUpperDifference(a.Close, a.Low) > TopologyLowerDifference(a.Open, a.Close)
            && TopologyLowerDifference(b.Open, b.Close) < TopologyUpperDifference(a.Open, a.Close);
    }
}
#endif

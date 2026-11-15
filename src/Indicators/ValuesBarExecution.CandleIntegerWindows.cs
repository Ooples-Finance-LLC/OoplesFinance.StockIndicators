#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private enum CandleIntegerKind { MatchingLow, StickSandwich, ThreeLineStrike, OnNeck, InNeck, Thrusting }

    private static bool TryCandleIntegerConfiguration(IIndicator indicator,
        out CandleIntegerKind kind, out int rangePeriod, out int bodyPeriod)
    {
        (kind, rangePeriod, bodyPeriod) = indicator switch
        {
            MatchingLowCandle c => (CandleIntegerKind.MatchingLow, c.Period, 0),
            StickSandwichCandle c => (CandleIntegerKind.StickSandwich, c.Period, 0),
            ThreeLineStrikeCandle c => (CandleIntegerKind.ThreeLineStrike, c.Period, 0),
            OnNeckCandle c => (CandleIntegerKind.OnNeck, c.EqualPeriod, c.BodyPeriod),
            InNeckCandle c => (CandleIntegerKind.InNeck, c.EqualPeriod, c.BodyPeriod),
            ThrustingCandle c => (CandleIntegerKind.Thrusting, c.EqualPeriod, c.BodyPeriod),
            _ => (default, 0, 0)
        };
        return rangePeriod is >= 1 and <= 64 && bodyPeriod <= 64;
    }

    // Private batch prototype: retain the existing worker ownership and validation
    // boundary. A block includes the complete threshold window and anchor candle.
    private static PointwiseRegion ComputeCandleIntegerWindows(Bar[] source, double[] output,
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
        if (!TryCandleIntegerConfiguration(indicator, out var kind, out int rangePeriod, out int bodyPeriod)) return false;
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
        // Multiplication by an exactly represented power of two performs the
        // same binary scaling. Extreme negative grids retain ScaleB rather
        // than constructing an infinite factor. Certified normalized prices
        // are integers, so accepted values neither underflow nor lose bits.
        double factor = grid >= -1023 ? Math.ScaleB(1, -grid) : double.PositiveInfinity;
        bool useFactor = double.IsFinite(factor) && factor > 0;
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
            double o, h, l, c;
            if (useFactor)
            {
                o = bar.Open * factor; h = bar.High * factor;
                l = bar.Low * factor; c = bar.Close * factor;
            }
            else
            {
                o = Math.ScaleB(bar.Open, -grid); h = Math.ScaleB(bar.High, -grid);
                l = Math.ScaleB(bar.Low, -grid); c = Math.ScaleB(bar.Close, -grid);
            }
            if (!(Math.Abs(o) <= priceLimit && Math.Abs(h) <= priceLimit
                && Math.Abs(l) <= priceLimit && Math.Abs(c) <= priceLimit)) return false;
            open[i] = (long)o; close[i] = (long)c; low[i] = (long)l;
            Int128 nextBody = (Int128)body[i] + Math.Abs(close[i] - open[i]);
            Int128 nextRange = (Int128)range[i] + (long)h - low[i];
            if (nextBody > long.MaxValue || nextRange < long.MinValue || nextRange > long.MaxValue) return false;
            body[i + 1] = (long)nextBody;
            range[i + 1] = (long)nextRange;
        }
        int warmup = indicator.WarmupBars;
        for (int i = prefix; i < n; i++)
        {
            double value = 0;
            if (i >= warmup)
            {
                int last = i - 1;
                if (kind == CandleIntegerKind.MatchingLow)
                {
                    if (close[last] < open[last] && close[i] < open[i]
                        && (Int128)Math.Abs(close[i] - close[last]) * (rangePeriod * 20)
                            <= (Int128)range[last] - range[last - rangePeriod])
                        value = 100;
                }
                else if (kind == CandleIntegerKind.StickSandwich)
                {
                    int middle = i - 2;
                    if (close[middle] < open[middle] && close[last] >= open[last] && close[i] < open[i]
                        && low[last] > close[middle]
                        && (Int128)Math.Abs(close[i] - close[middle]) * (rangePeriod * 20)
                            <= (Int128)range[middle] - range[middle - rangePeriod])
                        value = 100;
                }
                else if (kind == CandleIntegerKind.ThreeLineStrike)
                {
                    int middle = i - 2, first = i - 3;
                    bool white = close[last] >= open[last];
                    if ((close[first] >= open[first]) == white && (close[middle] >= open[middle]) == white
                        && (close[i] >= open[i]) != white
                        && CandleIntegerWithin(open[middle], open[first], close[first],
                            (Int128)range[first] - range[first - rangePeriod], rangePeriod * 5)
                        && CandleIntegerWithin(open[last], open[middle], close[middle],
                            (Int128)range[middle] - range[middle - rangePeriod], rangePeriod * 5))
                    {
                        if (white && close[last] > close[middle] && close[middle] > close[first]
                            && open[i] > close[last] && close[i] < open[first]) value = 100;
                        else if (!white && close[last] < close[middle] && close[middle] < close[first]
                            && open[i] < close[last] && close[i] > open[first]) value = -100;
                    }
                }
                else if (close[last] < open[last] && close[i] >= open[i] && open[i] < low[last]
                    && (Int128)(open[last] - close[last]) * bodyPeriod
                        > (Int128)body[last] - body[last - bodyPeriod])
                {
                    long center = kind == CandleIntegerKind.OnNeck ? low[last] : close[last];
                    if (kind == CandleIntegerKind.OnNeck || close[i] >= center)
                    {
                        Int128 margin = (Int128)range[last] - range[last - rangePeriod]
                            - (Int128)Math.Abs(close[i] - center) * (rangePeriod * 20);
                        if (kind != CandleIntegerKind.Thrusting ? margin >= 0
                            : margin < 0 && (Int128)open[last] + close[last] - 2 * (Int128)close[i] >= 0)
                            value = -100;
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

    private static bool CandleIntegerWithin(long price, long open, long close, Int128 range, int denominator) =>
        range - (Int128)(price - Math.Max(open, close)) * denominator >= 0
        && range - (Int128)(Math.Min(open, close) - price) * denominator >= 0;
}
#endif

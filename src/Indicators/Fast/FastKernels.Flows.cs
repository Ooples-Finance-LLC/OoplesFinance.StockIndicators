using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators.Fast;

internal static partial class FastKernels
{
    /// <summary>
    /// Difference of two exponential averages of the close or the volume, as a value or a percentage of the
    /// slow average, with its signal average and histogram. A zero slow average publishes no percentage and
    /// feeds the signal a zero.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void EmaDifference(FastColumns columns, double[][] outputs, EmaDifferenceSignal owner)
    {
        ReadOnlySpan<double> input = owner.Volume ? columns.Volume : columns.Close;
        var n = input.Length;
        var firstPrice = owner.FirstPrice;
        var percentage = owner.Percentage;
        var fast = new Seeded(owner.FastPeriod, firstPrice, 2.0 / (owner.FastPeriod + 1.0));
        var slow = new Seeded(owner.SlowPeriod, firstPrice, 2.0 / (owner.SlowPeriod + 1.0));
        var signal = new Seeded(owner.SignalPeriod, firstPrice, 2.0 / (owner.SignalPeriod + 1.0));
        long fastStart = firstPrice ? 0 : owner.FastPeriod - 1L, slowStart = firstPrice ? 0 : owner.SlowPeriod - 1L;
        var both = Math.Max(fastStart, slowStart);
        var signalStart = both + (firstPrice ? 0 : owner.SignalPeriod - 1L);
        var selection = owner.Selection;
        bool wantOscillator = (selection & EmaDifferenceSelection.Oscillator) != 0,
            wantSignal = (selection & EmaDifferenceSelection.Signal) != 0,
            wantHistogram = (selection & EmaDifferenceSelection.Histogram) != 0,
            wantFast = (selection & EmaDifferenceSelection.FastAverage) != 0,
            wantSlow = (selection & EmaDifferenceSelection.SlowAverage) != 0;
        for (var slot = 0; slot < 5; slot++) outputs[slot].AsSpan().Clear();
        Flag(outputs[5], wantOscillator, both);
        Flag(outputs[6], wantSignal, signalStart);
        Flag(outputs[7], wantHistogram, signalStart);
        Flag(outputs[8], wantFast, fastStart);
        Flag(outputs[9], wantSlow, slowStart);
        Span<double> oscillators = outputs[0], signals = outputs[1], histograms = outputs[2], fasts = outputs[3],
            slows = outputs[4];
        for (var i = 0; i < n; i++)
        {
            var value = input[i];
            fast.Add(value);
            slow.Add(value);
            if (wantFast && i >= fastStart) fasts[i] = fast.Value;
            if (wantSlow && i >= slowStart) slows[i] = slow.Value;
            if (i < both) continue;
            double oscillator;
            var present = true;
            if (!percentage)
                oscillator = fast.Value - slow.Value;
            else if (slow.Value != 0)
                oscillator = 100 * (fast.Value - slow.Value) / slow.Value;
            else
            {
                oscillator = 0;
                present = false;
                outputs[5][i] = 0;
                outputs[7][i] = 0;
            }
            if (wantOscillator && present) oscillators[i] = oscillator;
            signal.Add(oscillator);
            if (i < signalStart) continue;
            if (wantSignal) signals[i] = signal.Value;
            if (wantHistogram && present) histograms[i] = oscillator - signal.Value;
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    private static void Flag(Span<double> flags, bool selected, long first)
    {
        if (selected) DefinedFrom(flags, first);
        else flags.Clear();
    }

    /// <summary>
    /// Commodity channel index: the typical price's distance from its window mean, over 0.015 times the mean
    /// absolute deviation, taken either across the window or as the mean of the last distances.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void CommodityChannel(FastColumns columns, Span<double> value, Span<double> defined,
        WindowCommodityChannelIndex owner)
    {
        columns.RequireHighLowClose();
        ReadOnlySpan<double> high = columns.High, low = columns.Low, close = columns.Close;
        var n = close.Length;
        var period = owner.Period;
        var rolling = owner.RollingDeviations;
        var flatFlag = owner.FlatZero ? 1.0 : 0.0;
        value = value.Slice(0, n);
        defined = defined.Slice(0, n);
        value.Clear();
        defined.Clear();
        if (n < period) return;
        var typical = FastMath.Allocate(n);
        // Equal typical prices must compare equal, so they are rounded exactly as the exact route rounds them.
        FastMath.TypicalPrices(high, low, close, typical.AsSpan(0, n));
        // Distances of the last period bars, for the rolling form.
        var distances = rolling ? new double[period] : null;
        double anchor = typical[0], sum = 0, deviationSum = 0;
        // Consecutive equal typical prices: a window of them has a deviation of exactly zero.
        var run = 1;
        for (var i = 0; i < n; i++)
        {
            var price = typical[i];
            if (i > 0) run = price == typical[i - 1] ? run + 1 : 1; // NOSONAR an exact tie is the contract
            if (i >= period && (i & RebuildMask) == 0)
            {
                anchor = price;
                sum = 0;
                for (var j = i - period + 1; j <= i; j++) sum += typical[j] - anchor;
            }
            else
                sum += i >= period ? price - typical[i - period] : price - anchor;
            if (i < period - 1) continue;
            var flat = run >= period;
            var mean = flat ? price : anchor + sum / period;
            var distance = price - mean;
            double deviation;
            if (distances is not null)
            {
                var slot = (i - (period - 1)) % period;
                var absolute = Math.Abs(distance);
                var filled = i - (period - 1) >= period;
                deviationSum += filled ? absolute - distances[slot] : absolute;
                distances[slot] = absolute;
                if ((i & RebuildMask) == 0)
                {
                    deviationSum = 0;
                    for (var j = 0; j < period; j++) deviationSum += distances[j];
                }
                if (i - (period - 1) < period - 1) continue;
                // A run of two windows has only zero distances in the ring.
                if (run >= 2L * period - 1) deviationSum = 0;
                deviation = deviationSum;
            }
            else if (flat)
                deviation = 0;
            else
            {
                deviation = FastMath.DistanceSum(typical.AsSpan(i - period + 1, period), mean);
            }
            if (deviation == 0)
            {
                defined[i] = flatFlag;
                continue;
            }
            value[i] = 200.0 * period * distance / (3 * deviation);
            defined[i] = 1;
        }
    }

    /// <summary>
    /// Money flow index: the share of the window's typical-price-times-volume flow that came on bars whose
    /// typical price rose.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void MoneyFlow(FastColumns columns, Span<double> value, WindowMoneyFlowIndex owner)
    {
        columns.RequireHighLowClose();
        ReadOnlySpan<double> high = columns.High, low = columns.Low, close = columns.Close, volume = columns.Volume;
        var n = close.Length;
        var period = owner.Period;
        var minimumTotalOne = owner.MinimumTotalOne;
        var first = (long)period + owner.SuppressedBars;
        value = value.Slice(0, n);
        value.Clear();
        if (n == 0) return;
        // Each bar's flow on the side it joined, kept so it can leave the window again. The two series are
        // borrowed: a build would otherwise allocate two more than it publishes.
        var pool = System.Buffers.ArrayPool<double>.Shared;
        double[] ups = pool.Rent(n), downs = pool.Rent(n);
        ups[0] = downs[0] = 0;
        // The comparison decides which side a whole bar's flow joins. A plain typical price is within a few
        // units of the exactly rounded one, so it settles every comparison that is not that close; the rest
        // are settled by the prices rounded exactly as the exact route rounds them.
        // The largest flow of the series. A side with no flow in the window is exactly zero, which a sum that
        // has slid over larger flows no longer is: it is left a remainder far below them. A sum that small
        // beside the largest flow is therefore taken afresh, which is exact for a window with none.
        double largest = 0;
        var bar = 1;
#if !NETFRAMEWORK
        if (System.Numerics.Vector.IsHardwareAccelerated)
        {
            // A vector of bars at a time, each beside the bar before it. A group with any comparison too
            // close for the plain typical price to settle is left to the loop below.
            var width = System.Numerics.Vector<double>.Count;
            var third = new System.Numerics.Vector<double>(Third);
            var margin = new System.Numerics.Vector<double>(1e-15);
            var none = System.Numerics.Vector<double>.Zero;
            var most = none;
            for (; bar <= n - width; bar += width)
            {
                System.Numerics.Vector<double> h = new(high.Slice(bar, width)), l = new(low.Slice(bar, width)),
                    c = new(close.Slice(bar, width));
                System.Numerics.Vector<double> ph = new(high.Slice(bar - 1, width)), pl = new(low.Slice(bar - 1, width)),
                    pc = new(close.Slice(bar - 1, width));
                var typical = (h + l + c) * third;
                var move = typical - (ph + pl + pc) * third;
                var scales = System.Numerics.Vector.Abs(h) + System.Numerics.Vector.Abs(l) + System.Numerics.Vector.Abs(c)
                    + (System.Numerics.Vector.Abs(ph) + System.Numerics.Vector.Abs(pl) + System.Numerics.Vector.Abs(pc));
                if (!System.Numerics.Vector.GreaterThanAll(System.Numerics.Vector.Abs(move), margin * scales))
                {
                    for (var j = bar; j < bar + width; j++)
                        largest = Math.Max(largest, FlowOf(high, low, close, volume, j, ups, downs));
                    continue;
                }
                var flow = typical * new System.Numerics.Vector<double>(volume.Slice(bar, width));
                System.Numerics.Vector.ConditionalSelect(System.Numerics.Vector.GreaterThan(move, none), flow, none)
                    .CopyTo(ups.AsSpan(bar, width));
                System.Numerics.Vector.ConditionalSelect(System.Numerics.Vector.LessThan(move, none), flow, none)
                    .CopyTo(downs.AsSpan(bar, width));
                most = System.Numerics.Vector.Max(most, System.Numerics.Vector.Abs(flow));
            }
            for (var j = 0; j < width; j++) largest = Math.Max(largest, most[j]);
        }
#endif
        for (; bar < n; bar++) largest = Math.Max(largest, FlowOf(high, low, close, volume, bar, ups, downs));
        // The sums are a pass of their own: the bars that fill the first window, then stretches that only
        // slide, each begun on a sum taken afresh.
        var slight = 1e-6 * largest;
        double positive = 0, negative = 0;
        var at = 1;
        var filled = Math.Min(period, n - 1);
        for (; at <= filled; at++)
        {
            positive += ups[at];
            negative += downs[at];
            if (at >= first) value[at] = Share(positive, negative, minimumTotalOne);
        }
        while (at < n)
        {
            if ((at & RebuildMask) == 0)
            {
                positive = negative = 0;
                for (var j = at - period + 1; j <= at; j++)
                {
                    positive += ups[j];
                    negative += downs[j];
                }
                if (at >= first) value[at] = Share(positive, negative, minimumTotalOne);
                at++;
            }
            var end = Math.Min(n, (at | RebuildMask) + 1);
            for (; at < end; at++)
            {
                positive += ups[at] - ups[at - period];
                negative += downs[at] - downs[at - period];
                if (!(Math.Abs(positive) > slight) || !(Math.Abs(negative) > slight))
                {
                    // Through copies, so that the sums themselves are never addressed and stay in registers.
                    double fresh = positive, other = negative;
                    Resum(ups, downs, at - period + 1, at, ref fresh, ref other);
                    positive = fresh;
                    negative = other;
                }
                if (at >= first) value[at] = Share(positive, negative, minimumTotalOne);
            }
        }
        pool.Return(ups);
        pool.Return(downs);
    }

    private const double Third = 1.0 / 3;

    // One bar's flow, written to the side its typical price moved to, and its size.
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    private static double FlowOf(ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        ReadOnlySpan<double> volume, int i, double[] ups, double[] downs)
    {
        double h = high[i], l = low[i], c = close[i], ph = high[i - 1], pl = low[i - 1], pc = close[i - 1];
        var typical = (h + l + c) * Third;
        var move = typical - (ph + pl + pc) * Third;
        var scales = Math.Abs(h) + Math.Abs(l) + Math.Abs(c) + (Math.Abs(ph) + Math.Abs(pl) + Math.Abs(pc));
        if (!(Math.Abs(move) > 1e-15 * scales))
            move = FastMath.TypicalPrice(h, l, c).CompareTo(FastMath.TypicalPrice(ph, pl, pc));
        var flow = typical * volume[i];
        ups[i] = FastMath.KeepIf(flow, move > 0);
        downs[i] = FastMath.KeepIf(flow, move < 0);
        return Math.Abs(flow);
    }

    // The index for a window's two sums, or zero where the rule in force publishes none.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | FastMath.Hot)]
    private static double Share(double positive, double negative, bool minimumTotalOne)
    {
        var total = positive + negative;
        if (minimumTotalOne) return total >= 1 ? 100 * positive / total : 0;
        return negative == 0 ? 100 : total == 0 ? double.NegativeInfinity : 100 * positive / total; // NOSONAR exact zeros are the contract
    }

    // Out of line: it is taken only where a side of the window has next to no flow.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void Resum(double[] ups, double[] downs, int from, int to, ref double positive, ref double negative)
    {
        if (positive == 0 && negative == 0) return; // NOSONAR an exact zero is the contract
        double up = 0, down = 0;
        for (var j = from; j <= to; j++)
        {
            up += ups[j];
            down += downs[j];
        }
        positive = up;
        negative = down;
    }
}

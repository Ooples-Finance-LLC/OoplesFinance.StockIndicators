using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators.Fast;

internal static partial class FastKernels
{
    /// <summary>
    /// Difference of two exponential averages of the close or the volume, as a value or a percentage of the
    /// slow average, with its signal average and histogram. A zero slow average publishes no percentage and
    /// feeds the signal a zero.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
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
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
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
        for (var i = 0; i < n; i++) typical[i] = FastMath.TypicalPrice(high[i], low[i], close[i]);
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
                deviation = 0;
                for (var j = i - period + 1; j <= i; j++) deviation += Math.Abs(typical[j] - mean);
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
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
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
        // Each bar's flow on the side it joined, kept so it can leave the window again.
        double[] ups = FastMath.Allocate(n), downs = FastMath.Allocate(n);
        ups[0] = downs[0] = 0;
        // The comparison decides which side a whole bar's flow joins. A plain typical price is within a few
        // units of the exactly rounded one, so it settles every comparison that is not that close; the rest
        // are settled by the prices rounded exactly as the exact route rounds them.
        double previous = (high[0] + low[0] + close[0]) / 3, previousScale = Math.Abs(high[0]) + Math.Abs(low[0]) + Math.Abs(close[0]);
        double positive = 0, negative = 0;
        for (var i = 1; i < n; i++)
        {
            double h = high[i], l = low[i], c = close[i];
            double typical = (h + l + c) / 3, scale = Math.Abs(h) + Math.Abs(l) + Math.Abs(c);
            var move = typical - previous;
            if (!(Math.Abs(move) > 1e-15 * (scale + previousScale)))
                move = FastMath.TypicalPrice(h, l, c).CompareTo(FastMath.TypicalPrice(high[i - 1], low[i - 1], close[i - 1]));
            var flow = typical * volume[i];
            double up = FastMath.KeepIf(flow, move > 0), down = FastMath.KeepIf(flow, move < 0);
            previous = typical;
            previousScale = scale;
            ups[i] = up;
            downs[i] = down;
            if (i > period && (i & RebuildMask) == 0)
            {
                positive = negative = 0;
                for (var j = i - period + 1; j <= i; j++)
                {
                    positive += ups[j];
                    negative += downs[j];
                }
            }
            else if (i > period)
            {
                positive += up - ups[i - period];
                negative += down - downs[i - period];
            }
            else
            {
                positive += up;
                negative += down;
            }
            if (i < first) continue;
            var total = positive + negative;
            if (minimumTotalOne)
            {
                if (total >= 1) value[i] = 100 * positive / total;
            }
            else
                value[i] = negative == 0 ? 100 : total == 0 ? double.NegativeInfinity : 100 * positive / total;
        }
    }
}

namespace OoplesFinance.StockIndicators.Indicators.Fast;

internal static partial class FastKernels
{
    // Below this window a rescan when the extreme expires is cheaper than keeping a deque; above it the
    // deque bounds the cost on a series that falls (or rises) on every bar.
    private const int RescanLimit = 32;

    /// <summary>
    /// Highest or lowest of the last <paramref name="period"/> values, over the available history while the
    /// window is still filling.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    internal static void RollingExtreme(ReadOnlySpan<double> input, Span<double> output, int period, bool highest)
    {
        var n = input.Length;
        output = output.Slice(0, n);
        if (n == 0) return;
        if (period == 1)
        {
            input.CopyTo(output);
            return;
        }
#if !NETFRAMEWORK
        if (System.Numerics.Vector.IsHardwareAccelerated)
        {
            ExtremeByDoubling(input, output, period, highest);
            return;
        }
#endif
        if (period > RescanLimit)
        {
            ExtremeByDeque(input, output, period, highest);
            return;
        }
        var best = input[0];
        var at = 0;
        output[0] = best;
        for (var i = 1; i < n; i++)
        {
            var value = input[i];
            if (highest ? value >= best : value <= best)
            {
                best = value;
                at = i;
            }
            else if (at <= i - period)
            {
                at = i - period + 1;
                best = input[at];
                for (var j = at + 1; j <= i; j++)
                    if (highest ? input[j] >= best : input[j] <= best)
                    {
                        best = input[j];
                        at = j;
                    }
            }
            output[i] = best;
        }
    }

#if !NETFRAMEWORK
    // The extreme of the last 1, 2, 4, ... bars, each from the one before it, and then of the whole window
    // from two of those that overlap. Every step is one vector pass with no test in it, where following
    // the extreme bar by bar mispredicts each time prices set a new one or the old one leaves the window.
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    private static void ExtremeByDoubling(ReadOnlySpan<double> input, Span<double> output, int period, bool highest)
    {
        input.CopyTo(output);
        var n = output.Length;
        var span = 1;
        while (span <= period / 2 && span < n)
        {
            ExtremeWithLagged(output, span, highest);
            span *= 2;
        }
        // Past the series' own length the extreme of everything so far is already the answer.
        if (span < n && period - span > 0) ExtremeWithLagged(output, period - span, highest);
    }

    // Each value with the one a lag before it, newest bars first so that what is read is still unwritten.
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    private static void ExtremeWithLagged(Span<double> values, int lag, bool highest)
    {
        var n = values.Length;
        var width = System.Numerics.Vector<double>.Count;
        var i = n - width;
        for (; i >= lag; i -= width)
        {
            var now = new System.Numerics.Vector<double>(values.Slice(i, width));
            var then = new System.Numerics.Vector<double>(values.Slice(i - lag, width));
            // A compare and a select: the library's own maximum also orders zeros and NaNs, which costs several
            // times as much and matters to no finite price.
            var replaces = highest ? System.Numerics.Vector.GreaterThan(then, now) : System.Numerics.Vector.LessThan(then, now);
            System.Numerics.Vector.ConditionalSelect(replaces, then, now).CopyTo(values.Slice(i, width));
        }
        for (var j = Math.Min(n, i + width) - 1; j >= lag; j--)
        {
            double now = values[j], then = values[j - lag];
            values[j] = highest ? (then > now ? then : now) : (then < now ? then : now);
        }
    }
#endif

    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    private static void ExtremeByDeque(ReadOnlySpan<double> input, Span<double> output, int period, bool highest)
    {
        // Candidate indices, oldest first; values are monotone, so the head is always the extreme.
        // The queue is as long as the series: every bar enters once, so neither end ever wraps and no
        // index needs a remainder.
        var queue = System.Buffers.ArrayPool<int>.Shared.Rent(input.Length);
        int head = 0, tail = 0;
        for (var i = 0; i < input.Length; i++)
        {
            var value = input[i];
            while (tail > head)
            {
                var last = input[queue[tail - 1]];
                if (highest ? last > value : last < value) break;
                tail--;
            }
            if (tail > head && queue[head] <= i - period) head++;
            queue[tail++] = i;
            output[i] = input[queue[head]];
        }
        System.Buffers.ArrayPool<int>.Shared.Return(queue);
    }

    // One unit in the last place of a value near one.
    private const double Ulp = 2.220446049250313e-16;

    // The rounding the slid moments may carry, as a fraction of the spread they describe.
    private const double SpreadTolerance = 5.820766091346741e-11;

    /// <summary>
    /// Variance, deviation or z-score of the last <paramref name="owner"/>.Period closes, over the available
    /// history while the window fills.
    /// </summary>
    /// <remarks>
    /// The mean and the sum of squared deviations slide with the window, measured from an anchor price so
    /// that their rounding is on the scale of the deviations and not of the price. The window is summed
    /// again around a new anchor after every block of bars, which bounds the rounding a block can add to a
    /// block's worth of its largest update. If the spread falls so far below that largest update that the
    /// bound would show, the window is summed again at once. A window of equal values is recognised exactly.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    internal static void Dispersion(ReadOnlySpan<double> close, Span<double> output, WindowDispersion owner)
    {
        var n = close.Length;
        output = output.Slice(0, n);
        var period = owner.Period;
        var multiplier = owner.Multiplier;
        if (n == 0) return;
        if (period == 1 || multiplier == 0)
        {
            output.Clear();
            return;
        }
        var selected = owner.Output;
        var sample = owner.Sample;
        var block = Math.Max(64, period);
        // Below this multiple of the block's largest update, the block's rounding could reach the tolerance.
        var floor = block * Ulp / SpreadTolerance;
        double anchor = close[0], mean = 0, squares = 0, largest = 0;
        int run = 1, slid = 0;
        output[0] = 0;
        var filling = Math.Min(period, n);
        for (var i = 1; i < filling; i++)
        {
            var value = close[i];
            run = value == close[i - 1] ? run + 1 : 1; // NOSONAR an exact tie is the contract
            var count = i + 1;
            var offset = value - anchor;
            var delta = offset - mean;
            mean += delta / count;
            var term = delta * (offset - mean);
            squares += term;
            largest = FastMath.Larger(largest, Math.Abs(term));
            if (!(squares > largest * floor))
            {
                var moments = run >= count ? new SlidingMoments { Anchor = value } : SlidingMoments.Of(close.Slice(0, count));
                anchor = moments.Anchor;
                mean = moments.Mean;
                squares = moments.Squares;
                largest = 0;
                offset = 0;
            }
            output[i] = Reading(selected, squares / (sample ? count - 1 : count), offset - mean, multiplier);
        }
        var inverse = 1.0 / period;
        var divisor = sample ? period - 1.0 : period;
        var varianceScale = multiplier / divisor;
        // The full window: no branch in the common case but the one that guards the rounding.
        for (var i = filling; i < n; i++)
        {
            var value = close[i];
            run = value == close[i - 1] ? run + 1 : 1; // NOSONAR an exact tie is the contract
            var offset = value - anchor;
            var expired = close[i - period] - anchor;
            var delta = offset - expired;
            var prior = mean;
            mean += delta * inverse;
            var term = delta * (offset - mean + expired - prior);
            squares += term;
            largest = FastMath.Larger(largest, Math.Abs(term));
            if (++slid > block || !(squares > largest * floor))
            {
                var moments = run >= period
                    ? new SlidingMoments { Anchor = value } : SlidingMoments.Of(close.Slice(i - period + 1, period));
                anchor = moments.Anchor;
                mean = moments.Mean;
                squares = moments.Squares;
                largest = 0;
                slid = 0;
                offset = 0;
            }
            output[i] = selected == WindowDispersionOutput.Variance ? squares * varianceScale
                : Reading(selected, squares / divisor, offset - mean, multiplier);
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | FastMath.Hot)]
    private static double Reading(WindowDispersionOutput selected, double variance, double deviation, double multiplier) =>
        selected switch
        {
            WindowDispersionOutput.Variance => variance * multiplier,
            WindowDispersionOutput.StandardDeviation => multiplier * Math.Sqrt(variance),
            _ => variance > 0 ? deviation * multiplier / Math.Sqrt(variance) : 0,
        };

    // Mean and sum of squared deviations of a window, both measured from the anchor price.
    private struct SlidingMoments
    {
        internal double Anchor, Mean, Squares;

        // Sums the window around its newest value, which becomes the anchor.
        internal static SlidingMoments Of(ReadOnlySpan<double> window)
        {
            var anchor = window[window.Length - 1];
            double sum = 0;
            for (var i = 0; i < window.Length; i++) sum += window[i] - anchor;
            var mean = sum / window.Length;
            double squares = 0;
            for (var i = 0; i < window.Length; i++)
            {
                var deviation = window[i] - anchor - mean;
                squares += deviation * deviation;
            }
            return new SlidingMoments { Anchor = anchor, Mean = mean, Squares = squares };
        }
    }

    /// <summary>
    /// Position of the close inside the high/low window as a percentage, zero for a flat window, then the K
    /// average of that and the D average of K. Both are published once D is defined.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    internal static void Stochastic(FastColumns columns, FastOutputs outputs, ClassicStochastic owner)
    {
        columns.RequireHighLowClose();
        ReadOnlySpan<double> high = columns.High, low = columns.Low, close = columns.Close;
        var n = close.Length;
        Span<double> k = outputs[0], d = outputs[1];
        k = k.Slice(0, n);
        d = d.Slice(0, n);
        var first = owner.First;
        outputs.Flag(2, first);
        outputs.Flag(3, first);
        var ready = owner.Period - 1L;
        if (first >= n)
        {
            k.Clear();
            d.Clear();
            return;
        }
        var offset = (int)ready;
        var highest = FastMath.Allocate(n);
        var lowest = FastMath.Allocate(n);
        RollingExtreme(high, highest, owner.Period, true);
        RollingExtreme(low, lowest, owner.Period, false);
        // The raw position reuses the highest buffer.
        Span<double> raw = highest.AsSpan(offset, n - offset);
        for (var i = offset; i < n; i++)
        {
            var range = highest[i] - lowest[i];
            raw[i - offset] = range == 0 ? 0 : 100 * (close[i] - lowest[i]) / range;
        }
        var kFirst = owner.KAverage.First;
        var kStart = offset + (int)kFirst;
        ClassicAverage(raw, k.Slice(offset), owner.KAverage, kFirst);
        ClassicAverage(k.Slice(kStart), d.Slice(kStart), owner.DAverage, owner.DAverage.First);
        k.Slice(0, (int)first).Clear();
        d.Slice(0, kStart).Clear();
    }


    /// <summary>
    /// Population standard deviation over the window with the window mean, the z-score where the window is
    /// not flat, and an optional rolling mean of the deviation.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    internal static void StandardDeviationDetails(ReadOnlySpan<double> close, FastOutputs outputs, int period, int? smoothingPeriod)
    {
        var n = close.Length;
        for (var slot = 3; slot < 8; slot++) outputs[slot].AsSpan(0, n).Clear();
        Span<double> deviation = outputs[0].AsSpan(0, n), mean = outputs[1].AsSpan(0, n), score = outputs[2].AsSpan(0, n);
        if (n < period)
        {
            deviation.Clear();
            mean.Clear();
            score.Clear();
            return;
        }
        Dispersion(close, deviation, new WindowDispersion(period));
        Sma(close, mean, period);
        Dispersion(close, score, new WindowDispersion(period, WindowDispersionOutput.ZScore));
        var first = period - 1;
        deviation.Slice(0, first).Clear();
        mean.Slice(0, first).Clear();
        score.Slice(0, first).Clear();
        outputs.Flag(4, first);
        outputs.Flag(5, first);
        var scoreDefined = outputs[6].AsSpan(0, n);
        // The window has a variance unless every value in it is the same one.
        var run = 1;
        for (var i = 1; i < n; i++)
        {
            run = close[i] == close[i - 1] ? run + 1 : 1; // NOSONAR an exact tie is the contract
            if (i < first) continue;
            if (run >= period) score[i] = 0;
            else scoreDefined[i] = 1;
        }
        if (smoothingPeriod is not int window) return;
        Span<double> smooth = outputs[3].AsSpan(0, n), smoothDefined = outputs[7].AsSpan(0, n);
        double sum = 0;
        for (var i = first; i < n; i++)
        {
            var held = i - first + 1;
            if (held > window && ((i - first) & RebuildMask) == 0)
            {
                sum = 0;
                for (var j = i - window + 1; j <= i; j++) sum += deviation[j];
            }
            else
            {
                if (held > window) sum -= deviation[i - window];
                sum += deviation[i];
            }
            if (held < window) continue;
            smooth[i] = window == 1 ? deviation[i] : sum / window;
            smoothDefined[i] = 1;
        }
    }


    /// <summary>
    /// Moving-average envelope: the chosen average with a band a fixed percentage above and below it, from
    /// the first bar with a full window.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    internal static void AverageEnvelope(ReadOnlySpan<double> close, double[][] outputs, WindowAverageEnvelope owner)
    {
        var n = close.Length;
        var period = owner.Period;
        Span<double> center = outputs[0].AsSpan(0, n), upper = outputs[1].AsSpan(0, n), lower = outputs[2].AsSpan(0, n);
        var defined = outputs[3].AsSpan(0, n);
        upper.Clear();
        lower.Clear();
        defined.Clear();
        if (n <= owner.First)
        {
            center.Clear();
            return;
        }
        var first = (int)owner.First;
        switch (owner.Average)
        {
            case EnvelopeAverage.Wma: WeightedWindow(close, center, period); break;
            case EnvelopeAverage.Hma: HullCenter(close, center, new HullWindow(period)); break;
            // The second output is scratch here; it is cleared before the bands are written.
            case EnvelopeAverage.Alma: ArnaudLegoux(close, center, upper, new ArnaudLegouxWindow(period)); break;
            case EnvelopeAverage.Dema: SeededExponential(close, center, period, 2, DefaultSeedIsShared(period, 2)); break;
            case EnvelopeAverage.Tema: SeededExponential(close, center, period, 3, DefaultSeedIsShared(period, 3)); break;
            case EnvelopeAverage.Smma: WilderAverage(close, center, period, new WilderMovingAverage(period).SeedWithAverage); break;
            case EnvelopeAverage.Epma: LinearRegression(close, center, new WindowLinearRegression(period)); break;
            case EnvelopeAverage.Ema: RunningSeedEma(close, center, period); break;
            default: Sma(close, center, period); break;
        }
        center.Slice(0, first).Clear();
        upper.Clear();
        var percent = owner.PercentOffset;
        double above = (100 + percent) / 100, below = (100 - percent) / 100;
        for (var i = first; i < n; i++)
        {
            upper[i] = center[i] * above;
            lower[i] = center[i] * below;
        }
        defined.Slice(first).Fill(1);
    }

    // Twice the half-period weighted mean less the full one, weighted again over the root period; the last
    // stage starts at the first bar with a full window.
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    private static void HullCenter(ReadOnlySpan<double> close, Span<double> output, HullWindow owner)
    {
        var n = close.Length;
        var start = owner.Period - 1;
        var pool = System.Buffers.ArrayPool<double>.Shared;
        double[] rentedHalf = pool.Rent(n), rentedFull = pool.Rent(n);
        try
        {
            Span<double> half = rentedHalf.AsSpan(0, n), full = rentedFull.AsSpan(0, n);
            WeightedWindow(close, half, owner.HalfPeriod);
            WeightedWindow(close, full, owner.Period);
            for (var i = start; i < n; i++) half[i] = 2 * half[i] - full[i];
            WeightedWindow(half.Slice(start), output.Slice(start), owner.RootPeriod);
        }
        finally
        {
            pool.Return(rentedHalf);
            pool.Return(rentedFull);
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    private static bool DefaultSeedIsShared(int period, int order) =>
        new SeededExponentialAverage(period, order).SeedMode == ExponentialSeedMode.Shared;


    /// <summary>
    /// Sample skewness or sample excess kurtosis of the last period closes, over the bars so far while the
    /// window fills. Each window is summed around its own mean, so the result does not lose digits to the
    /// price level.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    internal static void SampleShape(ReadOnlySpan<double> close, Span<double> value, Span<double> defined, int period, bool kurtosis)
    {
        var n = close.Length;
        value = value.Slice(0, n);
        defined = defined.Slice(0, n);
        value.Clear();
        defined.Fill(1);
        var least = kurtosis ? 4 : 3;
        var run = 1;
        for (var i = 1; i < n; i++)
        {
            run = close[i] == close[i - 1] ? run + 1 : 1; // NOSONAR an exact tie is the contract
            var count = Math.Min(i + 1, period);
            if (count < least) continue;
            if (run >= count)
            {
                // A flat window has no shape; kurtosis reports it as absent.
                if (kurtosis) defined[i] = 0;
                continue;
            }
            var window = close.Slice(i - count + 1, count);
            var anchor = window[count - 1];
            double sum = 0;
            for (var j = 0; j < count; j++) sum += window[j] - anchor;
            var mean = sum / count;
            double second = 0, third = 0, fourth = 0;
            for (var j = 0; j < count; j++)
            {
                var deviation = window[j] - anchor - mean;
                var square = deviation * deviation;
                second += square;
                third += square * deviation;
                fourth += square * square;
            }
            double size = count;
            value[i] = kurtosis
                ? ((size * size - 1) * size * fourth / (second * second) - 3 * (size - 1) * (size - 1)) / ((size - 2) * (size - 3))
                : size * Math.Sqrt(size - 1) * third / ((size - 2) * second * Math.Sqrt(second));
        }
    }


    // Exponential average seeded by the mean of the first period values, written from the seed bar on.
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    private static void MeanSeededEma(ReadOnlySpan<double> input, Span<double> output, int period)
    {
        double sum = 0;
        for (var i = 0; i < period; i++) sum += input[i];
        var value = period == 1 ? input[0] : sum / period;
        output[period - 1] = value;
        FastMath.BlendRun((period - 1.0) / (period + 1.0), 2.0 / (period + 1.0), input, output, period, value);
    }

    /// <summary>
    /// Schaff trend cycle, single pass: where the fast-less-slow average difference sits in its rolling
    /// range, as a percentage, averaged over three bars.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    internal static void StochasticCycle(ReadOnlySpan<double> close, Span<double> value, Span<double> defined, int cycle,
        int fast, int slow)
    {
        var n = close.Length;
        value = value.Slice(0, n);
        defined = defined.Slice(0, n);
        value.Clear();
        defined.Clear();
        var start = slow - 1;
        var first = (long)start + cycle - 1 + 2;
        if (first >= n) return;
        var count = n - start;
        var pool = System.Buffers.ArrayPool<double>.Shared;
        double[] rentedFast = pool.Rent(n), rentedSlow = pool.Rent(n), rentedLow = pool.Rent(count), rentedHigh = pool.Rent(count);
        try
        {
            Span<double> fastAverage = rentedFast.AsSpan(0, n), slowAverage = rentedSlow.AsSpan(0, n);
            MeanSeededEma(close, fastAverage, fast);
            MeanSeededEma(close, slowAverage, slow);
            // The difference overwrites the slow average from the bar both exist; its size before the
            // subtraction bounds the rounding the two averages carry into it.
            var difference = slowAverage.Slice(start);
            double size = 0;
            for (var k = 0; k < count; k++)
            {
                size = FastMath.Larger(size, Math.Abs(fastAverage[start + k]) + Math.Abs(difference[k]));
                difference[k] = fastAverage[start + k] - difference[k];
            }
            var noise = 64 * Ulp * size;
            Span<double> lowest = rentedLow.AsSpan(0, count), highest = rentedHigh.AsSpan(0, count);
            RollingExtreme(difference, lowest, cycle, false);
            RollingExtreme(difference, highest, cycle, true);
            double a = 0, b = 0;
            for (var k = cycle - 1; k < count; k++)
            {
                var range = highest[k] - lowest[k];
                var position = range > noise ? 100 * (difference[k] - lowest[k]) / range : 0;
                if (k >= cycle + 1)
                {
                    value[start + k] = (a + b + position) / 3;
                    defined[start + k] = 1;
                }
                a = b;
                b = position;
            }
        }
        finally
        {
            pool.Return(rentedFast);
            pool.Return(rentedSlow);
            pool.Return(rentedLow);
            pool.Return(rentedHigh);
        }
    }


    /// <summary>
    /// Linearly weighted mean with the newest value at weight <paramref name="period"/>, over the bars so
    /// far while the window fills, divided by the weight actually held.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    internal static void ExpandingWeightedWindow(ReadOnlySpan<double> input, Span<double> output, int period)
    {
        var n = input.Length;
        double sum = 0, weighted = 0;
        for (var i = 0; i < n; i++)
        {
            var count = Math.Min(i + 1, period);
            if (i >= period && (i & RebuildMask) == 0)
            {
                sum = weighted = 0;
                for (var j = i - period + 1; j <= i; j++)
                {
                    sum += input[j];
                    weighted += (j - (i - period)) * input[j];
                }
            }
            else
            {
                // Every retained weight drops by one, then the new value enters at full weight.
                weighted += period * input[i] - sum;
                sum += input[i];
                if (i >= period) sum -= input[i - period];
            }
            output[i] = weighted / (0.5 * count * (2.0 * period - count + 1));
        }
    }

    /// <summary>
    /// Hull average: twice the half-period weighted mean less the full one, weighted again over the root
    /// period, either from the first bar or only from full windows.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    internal static void Hull(ReadOnlySpan<double> close, Span<double> value, Span<double> defined, HullWindow owner)
    {
        var n = close.Length;
        value = value.Slice(0, n);
        defined = defined.Slice(0, n);
        if (owner.Convention != HullWindowConvention.ExpandingFloor)
        {
            value.Clear();
            defined.Clear();
            var first = (long)owner.Period + owner.RootPeriod - 2;
            if (first >= n) return;
            HullCenter(close, value, owner);
            value.Slice(0, (int)first).Clear();
            defined.Slice((int)first).Fill(1);
            return;
        }
        defined.Fill(1);
        if (n == 0) return;
        var pool = System.Buffers.ArrayPool<double>.Shared;
        double[] rentedHalf = pool.Rent(n), rentedFull = pool.Rent(n);
        try
        {
            Span<double> half = rentedHalf.AsSpan(0, n), full = rentedFull.AsSpan(0, n);
            ExpandingWeightedWindow(close, half, owner.HalfPeriod);
            ExpandingWeightedWindow(close, full, owner.Period);
            for (var i = 0; i < n; i++) half[i] = 2 * half[i] - full[i];
            ExpandingWeightedWindow(half, value, owner.RootPeriod);
        }
        finally
        {
            pool.Return(rentedHalf);
            pool.Return(rentedFull);
        }
    }


    // Mean of the last period values of a series that is never negative, over the bars so far while the
    // window fills. A window holding only zeros reads exactly zero, and the sliding sum is taken afresh
    // once it has fallen well below the largest total it has held.
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    private static void MeanOfMagnitudes(ReadOnlySpan<double> input, Span<double> output, int period)
    {
        var n = input.Length;
        double sum = 0, peak = 0;
        var nonZero = 0;
        for (var i = 0; i < n; i++)
        {
            var count = Math.Min(i + 1, period);
            if (i >= period)
            {
                sum -= input[i - period];
                if (input[i - period] != 0) nonZero--; // NOSONAR an exact tie is the contract
            }
            sum += input[i];
            if (input[i] != 0) nonZero++; // NOSONAR an exact tie is the contract
            peak = FastMath.Larger(peak, sum);
            if (nonZero == 0) sum = peak = 0;
            else if (i >= period && ((i & RebuildMask) == 0 || sum < 0.01 * peak))
            {
                sum = 0;
                for (var j = i - period + 1; j <= i; j++) sum += input[j];
                peak = sum;
            }
            output[i] = sum / count;
        }
    }

    /// <summary>
    /// Relative volatility strength: the sample deviation of the upward closes' changes, averaged, as a
    /// percentage of that plus the same for the downward ones. The first close counts as a change from zero.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    internal static void RelativeVolatility(ReadOnlySpan<double> close, Span<double> output, int period)
    {
        var n = close.Length;
        output = output.Slice(0, n);
        if (n == 0) return;
        var pool = System.Buffers.ArrayPool<double>.Shared;
        double[] rentedUp = pool.Rent(n), rentedDown = pool.Rent(n), rentedDeviation = pool.Rent(n);
        try
        {
            Span<double> up = rentedUp.AsSpan(0, n), down = rentedDown.AsSpan(0, n), deviation = rentedDeviation.AsSpan(0, n);
            double previous = 0;
            for (var i = 0; i < n; i++)
            {
                var change = close[i] - previous;
                previous = close[i];
                up[i] = FastMath.KeepIf(change, change > 0);
                down[i] = FastMath.KeepIf(-change, change < 0);
            }
            var sample = new WindowDispersion(period, WindowDispersionOutput.StandardDeviation, true);
            Dispersion(up, deviation, sample);
            MeanOfMagnitudes(deviation, up, period);
            Dispersion(down, deviation, sample);
            MeanOfMagnitudes(deviation, down, period);
            for (var i = 0; i < n; i++)
            {
                var total = up[i] + down[i];
                output[i] = total == 0 ? 0 : 100 * up[i] / total; // NOSONAR an exact tie is the contract
            }
        }
        finally
        {
            pool.Return(rentedUp);
            pool.Return(rentedDown);
            pool.Return(rentedDeviation);
        }
    }


    /// <summary>
    /// Stochastic momentum: the close measured from the middle of the high/low window, from the first full
    /// window.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    internal static void StochasticMomentum(FastColumns columns, Span<double> value, Span<double> defined, int period)
    {
        columns.RequireHighLowClose();
        ReadOnlySpan<double> high = columns.High, low = columns.Low, close = columns.Close;
        var n = close.Length;
        value = value.Slice(0, n);
        defined = defined.Slice(0, n);
        defined.Clear();
        if (n < period)
        {
            value.Clear();
            return;
        }
        var pool = System.Buffers.ArrayPool<double>.Shared;
        var rented = pool.Rent(n);
        try
        {
            var lowest = rented.AsSpan(0, n);
            RollingExtreme(high, value, period, true);
            RollingExtreme(low, lowest, period, false);
            var first = period - 1;
            for (var i = first; i < n; i++) value[i] = close[i] - 0.5 * value[i] - 0.5 * lowest[i];
            value.Slice(0, first).Clear();
            defined.Slice(first).Fill(1);
        }
        finally
        {
            pool.Return(rented);
        }
    }

    /// <summary>
    /// Stochastic momentum index: the twice-smoothed distance of the close from the middle of the high/low
    /// window over half the twice-smoothed window range, with an exponential signal line.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    internal static void StochasticMomentumIndex(FastColumns columns, double[][] outputs, DoubleSmoothedStochasticMomentum owner)
    {
        columns.RequireHighLowClose();
        ReadOnlySpan<double> high = columns.High, low = columns.Low, close = columns.Close;
        var n = close.Length;
        for (var slot = 0; slot < 4; slot++) outputs[slot].AsSpan(0, n).Clear();
        var period = owner.Period;
        if (n < period) return;
        var pool = System.Buffers.ArrayPool<double>.Shared;
        double[] rentedHigh = pool.Rent(n), rentedLow = pool.Rent(n);
        try
        {
            Span<double> highest = rentedHigh.AsSpan(0, n), lowest = rentedLow.AsSpan(0, n);
            RollingExtreme(high, highest, period, true);
            RollingExtreme(low, lowest, period, false);
            Span<double> index = outputs[0].AsSpan(0, n), signal = outputs[1].AsSpan(0, n);
            Span<double> defined = outputs[2].AsSpan(0, n), signalDefined = outputs[3].AsSpan(0, n);
            double firstAlpha = 2.0 / (owner.FirstPeriod + 1.0), secondAlpha = 2.0 / (owner.SecondPeriod + 1.0);
            double signalAlpha = 2.0 / (owner.SignalPeriod + 1.0);
            double delta1 = 0, delta2 = 0, range1 = 0, range2 = 0, line = 0;
            var signalStarted = false;
            for (var i = period - 1; i < n; i++)
            {
                var range = highest[i] - lowest[i];
                var delta = close[i] - 0.5 * highest[i] - 0.5 * lowest[i];
                if (i == period - 1)
                {
                    delta1 = delta2 = delta;
                    range1 = range2 = range;
                }
                else
                {
                    delta1 += firstAlpha * (delta - delta1);
                    delta2 += secondAlpha * (delta1 - delta2);
                    range1 += firstAlpha * (range - range1);
                    range2 += secondAlpha * (range1 - range2);
                }
                if (range2 == 0) // NOSONAR an exact tie is the contract
                {
                    signalStarted = false;
                    continue;
                }
                var current = 200 * delta2 / range2;
                index[i] = current;
                defined[i] = 1;
                if (double.IsInfinity(current)) continue;
                line = signalStarted ? line + signalAlpha * (current - line) : current;
                signalStarted = true;
                signal[i] = line;
                signalDefined[i] = 1;
            }
        }
        finally
        {
            pool.Return(rentedHigh);
            pool.Return(rentedLow);
        }
    }
}

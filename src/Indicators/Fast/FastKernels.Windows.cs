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

    private static void ExtremeByDeque(ReadOnlySpan<double> input, Span<double> output, int period, bool highest)
    {
        // Candidate indices, oldest first; values are monotone, so the head is always the extreme.
        var ring = new int[Math.Min(period, input.Length)];
        int head = 0, count = 0;
        for (var i = 0; i < input.Length; i++)
        {
            var value = input[i];
            while (count > 0)
            {
                var last = input[ring[(head + count - 1) % ring.Length]];
                if (highest ? last > value : last < value) break;
                count--;
            }
            if (count > 0 && ring[head] <= i - period)
            {
                head = (head + 1) % ring.Length;
                count--;
            }
            ring[(head + count) % ring.Length] = i;
            count++;
            output[i] = input[ring[head]];
        }
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

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
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
    internal static void Stochastic(FastColumns columns, double[][] outputs, ClassicStochastic owner)
    {
        columns.RequireHighLowClose();
        ReadOnlySpan<double> high = columns.High, low = columns.Low, close = columns.Close;
        var n = close.Length;
        Span<double> k = outputs[0], d = outputs[1];
        k = k.Slice(0, n);
        d = d.Slice(0, n);
        var first = owner.First;
        DefinedFrom(outputs[2], first);
        DefinedFrom(outputs[3], first);
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
    internal static void StandardDeviationDetails(ReadOnlySpan<double> close, double[][] outputs, int period, int? smoothingPeriod)
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
        outputs[4].AsSpan(first, n - first).Fill(1);
        outputs[5].AsSpan(first, n - first).Fill(1);
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

    private static bool DefaultSeedIsShared(int period, int order) =>
        new SeededExponentialAverage(period, order).SeedMode == ExponentialSeedMode.Shared;


    /// <summary>
    /// Sample skewness or sample excess kurtosis of the last period closes, over the bars so far while the
    /// window fills. Each window is summed around its own mean, so the result does not lose digits to the
    /// price level.
    /// </summary>
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
}

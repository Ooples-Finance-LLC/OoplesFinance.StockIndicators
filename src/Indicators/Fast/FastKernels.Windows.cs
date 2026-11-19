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

    // The slid moments are rebuilt from the window once their error bound passes this fraction of the result.
    private const double SpreadTolerance = 5.820766091346741e-11;

    /// <summary>
    /// Variance, deviation or z-score of the last <paramref name="owner"/>.Period closes, over the available
    /// history while the window fills.
    /// </summary>
    /// <remarks>
    /// The mean and the sum of squared deviations slide with the window, measured from an anchor price so
    /// that their rounding is on the scale of the deviations and not of the price. A bound on the rounding
    /// they have absorbed is carried along; when it becomes a visible fraction of the spread, the window is
    /// summed again around a new anchor. A window of equal values is recognised exactly.
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
        var score = selected == WindowDispersionOutput.ZScore;
        var inverse = 1.0 / period;
        var moments = new SlidingMoments { Anchor = close[0] };
        var run = 1;
        output[0] = 0;
        for (var i = 1; i < n; i++)
        {
            var value = close[i];
            run = value == close[i - 1] ? run + 1 : 1;
            var count = i < period ? i + 1 : period;
            if (run >= count)
            {
                moments = new SlidingMoments { Anchor = value };
                output[i] = 0;
                continue;
            }
            var offset = value - moments.Anchor;
            // The subtraction above is exact only while the price stays within half the anchor of it.
            var rebuild = moments.Anchor != 0 && !(Math.Abs(offset) <= 0.5 * Math.Abs(moments.Anchor));
            if (!rebuild)
            {
                double delta, term;
                if (i < period)
                {
                    delta = offset - moments.Mean;
                    moments.Mean += delta / count;
                    term = delta * (offset - moments.Mean);
                }
                else
                {
                    var expired = close[i - period] - moments.Anchor;
                    delta = offset - expired;
                    var prior = moments.Mean;
                    moments.Mean += delta * inverse;
                    term = delta * (offset - moments.Mean + expired - prior);
                }
                moments.Squares += term;
                moments.MeanError += Ulp * Math.Abs(moments.Mean);
                moments.Error += Ulp * (Math.Abs(term) + moments.Squares) + 2 * Math.Abs(delta) * moments.MeanError;
                rebuild = !(moments.Squares > 0) || moments.Error > moments.Squares * SpreadTolerance
                    || score && moments.MeanError * moments.MeanError * count
                        > moments.Squares * (SpreadTolerance * SpreadTolerance);
            }
            if (rebuild)
            {
                moments = SlidingMoments.Of(close.Slice(i - count + 1, count));
                offset = 0;
            }
            var variance = moments.Squares / (sample ? count - 1 : count);
            output[i] = selected switch
            {
                WindowDispersionOutput.Variance => variance * multiplier,
                WindowDispersionOutput.StandardDeviation => multiplier * Math.Sqrt(variance),
                _ => (offset - moments.Mean) * multiplier / Math.Sqrt(variance),
            };
        }
    }

    // Mean and sum of squared deviations of a window, both measured from the anchor price.
    private struct SlidingMoments
    {
        internal double Anchor, Mean, Squares, MeanError, Error;

        // Sums the window around its newest value, which becomes the anchor.
        internal static SlidingMoments Of(ReadOnlySpan<double> window)
        {
            var anchor = window[window.Length - 1];
            double sum = 0, largest = 0;
            for (var i = 0; i < window.Length; i++)
            {
                var offset = window[i] - anchor;
                sum += offset;
                largest = Math.Max(largest, Math.Abs(offset));
            }
            var mean = sum / window.Length;
            double squares = 0;
            for (var i = 0; i < window.Length; i++)
            {
                var deviation = window[i] - anchor - mean;
                squares += deviation * deviation;
            }
            return new SlidingMoments
            {
                Anchor = anchor, Mean = mean, Squares = squares,
                MeanError = Ulp * largest, Error = 4 * Ulp * squares,
            };
        }
    }

    /// <summary>
    /// Position of the close inside the high/low window as a percentage, zero for a flat window, then the K
    /// average of that and the D average of K. Both are published once D is defined.
    /// </summary>
    internal static void Stochastic(FastColumns columns, double[][] outputs, ClassicStochastic owner)
    {
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
}

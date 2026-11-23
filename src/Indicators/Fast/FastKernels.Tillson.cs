namespace OoplesFinance.StockIndicators.Indicators.Fast;

internal static partial class FastKernels
{
    /// <summary>
    /// Tillson T3: six cascaded exponential averages combined through the volume-factor polynomial.
    /// The seed decides how the stages start: all from the first price, from running prefix means, or each
    /// from the simple mean of the first period values the stage before it publishes.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void Tillson(ReadOnlySpan<double> input, Span<double> value, FastOutputs outputs, TillsonAverage owner)
    {
        var n = input.Length;
        value = value.Slice(0, n);
        var period = owner.Period;
        var seed = owner.Seed;
        var cascaded = seed == TillsonSeed.CascadedMeans;
        var prefix = seed == TillsonSeed.FollowingPrefix;
        var first = (cascaded ? 6L * (period - 1) : 0) + owner.Suppression;
        outputs.Flag(1, first);
        var factor = owner.VolumeFactor;
        var plus = 1 + factor;
        double k0 = plus * plus * plus, k1 = -3 * factor * plus * plus, k2 = 3 * factor * factor * plus,
            k3 = -factor * factor * factor;
        var alpha = 2.0 / (period + 1.0);
        var weight = 1.0 - alpha;
        Span<double> stage = stackalloc double[6];
        Span<double> sums = stackalloc double[6];
        stage.Clear();
        sums.Clear();
        // After this bar every stage is in its plain recurrence.
        var steady = cascaded ? 6L * (period - 1) + 1 : prefix ? period : 1L;
        var i = 0;
        for (; i < n && i < steady; i++)
        {
            value[i] = 0;
            var v = input[i];
            var complete = true;
            for (var j = 0; j < 6; j++)
            {
                if (cascaded)
                {
                    var start = (long)j * (period - 1);
                    if (i < start) { complete = false; break; }
                    if (i < start + period)
                    {
                        sums[j] += v;
                        if (i != start + period - 1) { complete = false; break; }
                        stage[j] = sums[j] / period;
                        sums[j] = 0;
                    }
                    else stage[j] = FastMath.Blend(weight, stage[j], alpha * v);
                }
                else if (i == 0) stage[j] = v;
                else if (prefix && i < period)
                {
                    sums[j] += v;
                    stage[j] = sums[j] / i;
                }
                else stage[j] = FastMath.Blend(weight, stage[j], alpha * v);
                v = stage[j];
            }
            if (complete && i >= first) value[i] = k0 * stage[2] + k1 * stage[3] + k2 * stage[4] + k3 * stage[5];
        }
        double e0 = stage[0], e1 = stage[1], e2 = stage[2], e3 = stage[3], e4 = stage[4], e5 = stage[5];
        for (; i < n; i++)
        {
            // Each stage waits on the one before it. Its own decayed value does not, so that is the product
            // taken first and the stage before joins in the one fused step: six steps deep a bar, not twelve.
            e0 = FastMath.Blend(alpha, input[i], weight * e0);
            e1 = FastMath.Blend(alpha, e0, weight * e1);
            e2 = FastMath.Blend(alpha, e1, weight * e2);
            e3 = FastMath.Blend(alpha, e2, weight * e3);
            e4 = FastMath.Blend(alpha, e3, weight * e4);
            e5 = FastMath.Blend(alpha, e4, weight * e5);
            value[i] = i >= first ? k0 * e2 + k1 * e3 + k2 * e4 + k3 * e5 : 0;
        }
    }
}

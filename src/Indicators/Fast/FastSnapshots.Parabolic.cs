namespace OoplesFinance.StockIndicators.Indicators.Fast;

/// <summary>
/// Plain double versions of the snapshot calculations. Each follows the recurrence of its exact
/// counterpart step for step; only the arithmetic differs, so a result can differ in its last digits.
/// </summary>
internal static partial class FastSnapshots
{
    /// <summary>The rows as a span, without a copy when the list is an array or a list.</summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    internal static ReadOnlySpan<Bar> Rows(IReadOnlyList<Bar> bars) => bars switch
    {
        Bar[] array => array,
#if NET5_0_OR_GREATER
        List<Bar> list => System.Runtime.InteropServices.CollectionsMarshal.AsSpan(list),
#endif
        _ => bars.ToArray(),
    };

    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    private static double Finite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value)
            ? value
            : throw new OverflowException("Parabolic stop is not representable.");

    /// <summary>The classic and the extended parabolic stop.</summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static double?[] ParabolicClassic(IReadOnlyList<Bar> bars, bool signed, double start, double offset,
        double initialLong, double stepLong, double maximumLong, double initialShort, double stepShort, double maximumShort)
    {
        var count = bars.Count;
        var result = new double?[count];
        if (count < 2) return result;
        var rows = Rows(bars);
        initialLong = Math.Min(initialLong, maximumLong);
        initialShort = Math.Min(initialShort, maximumShort);
        stepLong = Math.Min(stepLong, maximumLong);
        stepShort = Math.Min(stepShort, maximumShort);
        double firstHigh = rows[0].High, firstLow = rows[0].Low, priorHigh = rows[1].High, priorLow = rows[1].Low;
        var down = firstLow - priorLow;
        var rising = start == 0 ? !(down > 0 && down > priorHigh - firstHigh) : start > 0; // NOSONAR zero selects the automatic start
        var extreme = rising ? priorHigh : priorLow;
        var stop = start == 0 ? rising ? firstLow : firstHigh : Math.Abs(start); // NOSONAR zero selects the automatic start
        var factor = rising ? initialLong : initialShort;
        // A trend is a loop of its own, so nothing in it asks which way the trend runs, and whether a bar
        // extends the trend - as good as random - is settled without a branch. Only a reversal leaves the loop.
        // The clamps name the stop second: a stop that is not a number then stays one and is caught below.
        long bits = 0;
        var i = 1;
        while (i < count)
        {
            if (rising)
            {
                for (; i < count; i++)
                {
                    ref readonly var bar = ref rows[i];
                    double high = bar.High, low = bar.Low;
                    if (low <= stop) break;
                    result[i] = stop;
                    bits |= FastMath.NonFiniteBits(stop);
                    // The factor never exceeds its maximum, so a step of zero leaves it as it is.
                    factor = FastMath.Smaller(factor + FastMath.KeepIf(stepLong, high > extreme), maximumLong);
                    extreme = FastMath.Larger(extreme, high);
                    stop += factor * (extreme - stop);
                    stop = FastMath.Smaller(FastMath.Smaller(priorLow, low), stop);
                    priorHigh = high;
                    priorLow = low;
                }
                if (i == count) break;
                {
                    ref readonly var bar = ref rows[i];
                    double high = bar.High, low = bar.Low;
                    rising = false;
                    stop = FastMath.Larger(FastMath.Larger(priorHigh, high), extreme) * (1 + offset);
                    factor = initialShort;
                    extreme = low;
                    var published = signed ? -stop : stop;
                    result[i] = published;
                    bits |= FastMath.NonFiniteBits(published);
                    stop += factor * (extreme - stop);
                    stop = FastMath.Larger(FastMath.Larger(priorHigh, high), stop);
                    priorHigh = high;
                    priorLow = low;
                    i++;
                }
            }
            else
            {
                for (; i < count; i++)
                {
                    ref readonly var bar = ref rows[i];
                    double high = bar.High, low = bar.Low;
                    if (high >= stop) break;
                    var published = signed ? -stop : stop;
                    result[i] = published;
                    bits |= FastMath.NonFiniteBits(published);
                    factor = FastMath.Smaller(factor + FastMath.KeepIf(stepShort, low < extreme), maximumShort);
                    extreme = FastMath.Smaller(extreme, low);
                    stop += factor * (extreme - stop);
                    stop = FastMath.Larger(FastMath.Larger(priorHigh, high), stop);
                    priorHigh = high;
                    priorLow = low;
                }
                if (i == count) break;
                {
                    ref readonly var bar = ref rows[i];
                    double high = bar.High, low = bar.Low;
                    rising = true;
                    stop = FastMath.Smaller(FastMath.Smaller(priorLow, low), extreme) * (1 - offset);
                    factor = initialLong;
                    extreme = high;
                    result[i] = stop;
                    bits |= FastMath.NonFiniteBits(stop);
                    stop += factor * (extreme - stop);
                    stop = FastMath.Smaller(FastMath.Smaller(priorLow, low), stop);
                    priorHigh = high;
                    priorLow = low;
                    i++;
                }
            }
        }
        if (bits != 0) throw new OverflowException("Parabolic stop is not representable.");
        return result;
    }

    /// <summary>The parabolic stop that publishes nothing until its first reversal confirms a trend.</summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    internal static ConfirmedParabolicValue[] ParabolicConfirmed(IReadOnlyList<Bar> bars, double step, double maximum,
        double initial)
    {
        var count = bars.Count;
        var result = new ConfirmedParabolicValue[count];
        if (count == 0) return result;
        var rows = Rows(bars);
        var absent = new ConfirmedParabolicValue(null, null);
        result[0] = absent;
        var rising = true;
        double extreme = rows[0].High, stop = rows[0].Low, factor = initial;
        double priorHigh = extreme, priorLow = stop, olderHigh = priorHigh, olderLow = priorLow;
        var confirmed = false;
        for (var i = 1; i < count; i++)
        {
            ref readonly var bar = ref rows[i];
            double high = bar.High, low = bar.Low;
            stop += factor * (extreme - stop);
            if (i >= 2)
                stop = rising ? Math.Min(stop, Math.Min(priorLow, olderLow)) : Math.Max(stop, Math.Max(priorHigh, olderHigh));
            var reversal = rising ? low < stop : high > stop;
            // The bar of the first reversal is still unpublished; every later bar carries a stop.
            var publish = confirmed;
            if (reversal)
            {
                stop = extreme;
                rising = !rising;
                extreme = rising ? high : low;
                factor = initial;
                confirmed = true;
            }
            else if (rising ? high > extreme : low < extreme)
            {
                extreme = rising ? high : low;
                factor = Math.Min(factor + step, maximum);
            }
            result[i] = publish ? new ConfirmedParabolicValue(Finite(stop), reversal) : absent;
            olderHigh = priorHigh;
            olderLow = priorLow;
            priorHigh = high;
            priorLow = low;
        }
        return result;
    }

    /// <summary>The parabolic stop seeded from the extremes of the first five bars.</summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    internal static double?[] ParabolicFourBar(IReadOnlyList<Bar> bars, double step, double maximum)
    {
        var count = bars.Count;
        var result = new double?[count];
        if (count < 5) return result;
        var rows = Rows(bars);
        Bar third = rows[3], fourth = rows[4];
        var rising = fourth.High > third.High && fourth.Low > third.Low;
        if (!rising && !(fourth.High < third.High && fourth.Low < third.Low))
            rising = ParabolicStopSnapshots.SumIsGreater(fourth.High, fourth.Low, third.High, third.Low);
        double highest = rows[0].High, lowest = rows[0].Low;
        for (var i = 1; i < 5; i++)
        {
            highest = Math.Max(highest, rows[i].High);
            lowest = Math.Min(lowest, rows[i].Low);
        }
        double stop = rising ? lowest : highest, extreme = rising ? highest : lowest, factor = step;
        double priorHigh = fourth.High, priorLow = fourth.Low, olderHigh = third.High, olderLow = third.Low;
        result[4] = Finite(stop);
        for (var i = 5; i < count; i++)
        {
            ref readonly var bar = ref rows[i];
            double high = bar.High, low = bar.Low;
            var unchanged = rising ? priorLow > stop : priorHigh < stop;
            if (!unchanged)
            {
                stop = extreme;
                rising = !rising;
                extreme = rising ? Math.Max(extreme, high) : Math.Min(extreme, low);
                factor = step;
            }
            else
            {
                stop += factor * (extreme - stop);
                stop = rising ? Math.Min(stop, Math.Min(priorLow, olderLow)) : Math.Max(stop, Math.Max(priorHigh, olderHigh));
                var next = rising ? high : low;
                var fresh = rising ? next > extreme : next < extreme;
                extreme = rising ? Math.Max(extreme, next) : Math.Min(extreme, next);
                if (fresh && (rising ? low > stop : high < stop)) factor = Math.Min(factor + step, maximum);
            }
            result[i] = Finite(stop);
            olderHigh = priorHigh;
            olderLow = priorLow;
            priorHigh = high;
            priorLow = low;
        }
        return result;
    }
}

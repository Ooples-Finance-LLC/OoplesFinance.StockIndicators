using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Nullable stop and reversal flag after first-trend confirmation.</summary>
public sealed record ConfirmedParabolicValue(double? Sar, bool? IsReversal);

/// <summary>Parabolic stop conventions with exact comparisons and wide once-rounded updates.</summary>
public static class ParabolicStopSnapshots
{
    private static ParabolicStopValue U(double value) => ParabolicStopValue.Of(value);
    private static ParabolicStopValue Advance(ParabolicStopValue stop, ParabolicStopValue extreme, ParabolicStopValue factor) =>
        ParabolicStopValue.Advance(stop, extreme, factor);
    private static ParabolicStopValue Increase(ParabolicStopValue factor, ParabolicStopValue step, ParabolicStopValue maximum) =>
        ParabolicStopValue.Increase(factor, step, maximum);
    private static double Publish(ParabolicStopValue value) => value.Publish();

    private static void Validate(IReadOnlyList<Bar> bars, params double[] parameters)
    {
        if (bars is null) throw new ArgumentNullException(nameof(bars));
        if (parameters.Any(v => !FrameworkCompatibility.IsFinite(v)))
            throw new ArgumentOutOfRangeException(nameof(parameters));
        foreach (var b in bars)
            if (!FrameworkCompatibility.IsFinite(b.High) || !FrameworkCompatibility.IsFinite(b.Low))
                throw new ArgumentOutOfRangeException(nameof(bars));
    }

    /// <summary>Unsigned stop with directional-movement initialization and nonnegative acceleration/maximum.</summary>
    public static IReadOnlyList<double?> Classic(
        IReadOnlyList<Bar> bars,
        double acceleration = .02,
        double maximum = .2
    ) =>
        CalculateClassic(
            bars,
            false,
            0,
            0,
            acceleration,
            acceleration,
            maximum,
            acceleration,
            acceleration,
            maximum
        );

    /// <summary>Signed stops with independent long/short acceleration, optional signed start, and reversal offset.</summary>
    public static IReadOnlyList<double?> Extended(
        IReadOnlyList<Bar> bars,
        double startValue = 0,
        double offsetOnReverse = 0,
        double initialLong = .02,
        double stepLong = .02,
        double maximumLong = .2,
        double initialShort = .02,
        double stepShort = .02,
        double maximumShort = .2
    ) =>
        CalculateClassic(
            bars,
            true,
            startValue,
            offsetOnReverse,
            initialLong,
            stepLong,
            maximumLong,
            initialShort,
            stepShort,
            maximumShort
        );

    private static IReadOnlyList<double?> CalculateClassic(
        IReadOnlyList<Bar> bars,
        bool signed,
        double start,
        double offset,
        double initialLong,
        double stepLong,
        double maximumLong,
        double initialShort,
        double stepShort,
        double maximumShort
    )
    {
        Validate(
            bars,
            start,
            offset,
            initialLong,
            stepLong,
            maximumLong,
            initialShort,
            stepShort,
            maximumShort
        );
        if (
            new[]
            {
                offset,
                initialLong,
                stepLong,
                maximumLong,
                initialShort,
                stepShort,
                maximumShort,
            }.Any(v => v < 0)
        )
            throw new ArgumentOutOfRangeException(nameof(initialLong));
        var result = new double?[bars.Count];
        if (bars.Count < 2)
            return result;
        var initial = new[]
        {
            U(Math.Min(initialShort, maximumShort)),
            U(Math.Min(initialLong, maximumLong)),
        };
        var step = new[]
        {
            U(Math.Min(stepShort, maximumShort)),
            U(Math.Min(stepLong, maximumLong)),
        };
        var maximum = new[] { U(maximumShort), U(maximumLong) };
        var offsetUnits = U(offset);
        var down = U(bars[0].Low) - U(bars[1].Low);
        var upMove = U(bars[1].High) - U(bars[0].High);
        var rising = start == 0 ? !(down.Sign > 0 && down > upMove) : start > 0;
        var extreme = U(rising ? bars[1].High : bars[1].Low);
        var stop = U(
            start == 0
                ? rising
                    ? bars[0].Low
                    : bars[0].High
                : Math.Abs(start)
        );
        var factor = initial[rising ? 1 : 0];
        var priorHigh = U(bars[1].High);
        var priorLow = U(bars[1].Low);
        for (var i = 1; i < bars.Count; i++)
        {
            var high = U(bars[i].High);
            var low = U(bars[i].Low);
            var reversal = rising ? low <= stop : high >= stop;
            if (reversal)
            {
                rising = !rising;
                stop = rising
                    ? ParabolicStopValue.Min(extreme, ParabolicStopValue.Min(priorLow, low))
                    : ParabolicStopValue.Max(extreme, ParabolicStopValue.Max(priorHigh, high));
                stop = ParabolicStopValue.Offset(stop, offsetUnits, rising);
                factor = initial[rising ? 1 : 0];
                extreme = rising ? high : low;
            }
            result[i] = Publish(signed && !rising ? -stop : stop);
            if (!reversal && (rising ? high > extreme : low < extreme))
            {
                extreme = rising ? high : low;
                factor = Increase(factor, step[rising ? 1 : 0], maximum[rising ? 1 : 0]);
            }
            stop = Advance(stop, extreme, factor);
            stop = rising
                ? ParabolicStopValue.Min(stop, ParabolicStopValue.Min(priorLow, low))
                : ParabolicStopValue.Max(stop, ParabolicStopValue.Max(priorHigh, high));
            priorHigh = high;
            priorLow = low;
        }
        return result;
    }

    /// <summary>Skender convention: strict reversal, update before testing, and removal through the first reversal.</summary>
    public static IReadOnlyList<ConfirmedParabolicValue> Confirmed(
        IReadOnlyList<Bar> bars,
        double step = .02,
        double maximum = .2,
        double? initialFactor = null
    )
    {
        var initial = initialFactor ?? step;
        Validate(bars, step, maximum, initial);
        if (step <= 0 || maximum < step || initial <= 0 || initial > maximum)
            throw new ArgumentOutOfRangeException(nameof(step));
        var result = new ConfirmedParabolicValue[bars.Count];
        if (bars.Count == 0)
            return result;
        var stops = new ParabolicStopValue[bars.Count];
        var reversals = new bool[bars.Count];
        var rising = true;
        var extreme = U(bars[0].High);
        var stop = U(bars[0].Low);
        var initialUnits = U(initial);
        var stepUnits = U(step);
        var maximumUnits = U(maximum);
        var factor = initialUnits;
        var priorHigh = extreme;
        var priorLow = stop;
        var olderHigh = priorHigh;
        var olderLow = priorLow;
        var first = -1;
        for (var i = 1; i < bars.Count; i++)
        {
            var high = U(bars[i].High);
            var low = U(bars[i].Low);
            stop = Advance(stop, extreme, factor);
            if (i >= 2)
                stop = rising
                    ? ParabolicStopValue.Min(stop, ParabolicStopValue.Min(priorLow, olderLow))
                    : ParabolicStopValue.Max(stop, ParabolicStopValue.Max(priorHigh, olderHigh));
            var reversal = rising ? low < stop : high > stop;
            if (reversal)
            {
                stop = extreme;
                rising = !rising;
                extreme = rising ? high : low;
                factor = initialUnits;
                if (first < 0)
                    first = i;
            }
            else if (rising ? high > extreme : low < extreme)
            {
                extreme = rising ? high : low;
                factor = Increase(factor, stepUnits, maximumUnits);
            }
            stops[i] = stop;
            reversals[i] = reversal;
            olderHigh = priorHigh;
            olderLow = priorLow;
            priorHigh = high;
            priorLow = low;
        }
        // Publication remains after the full recurrence, including overflow checks.
        for (var i = 0; i < (first < 0 ? bars.Count : first + 1); i++)
            result[i] = new(null, null);
        if (first >= 0)
            for (var i = first + 1; i < bars.Count; i++)
                result[i] = new(Publish(stops[i]), reversals[i]);
        return result;
    }

    /// <summary>Trady convention: initialize on bar five, test the previous candle for reversal, and allow finite signed steps.</summary>
    public static IReadOnlyList<double?> FourBar(
        IReadOnlyList<Bar> bars,
        double step = .02,
        double maximum = .2
    )
    {
        Validate(bars, step, maximum);
        if (step > maximum)
            throw new ArgumentOutOfRangeException(nameof(step));
        var result = new double?[bars.Count];
        if (bars.Count < 5)
            return result;
        var rising = bars[4].High > bars[3].High && bars[4].Low > bars[3].Low;
        if (!rising && !(bars[4].High < bars[3].High && bars[4].Low < bars[3].Low))
            rising = U(bars[4].High) + U(bars[4].Low) > U(bars[3].High) + U(bars[3].Low);
        var high = bars.Take(5).Max(b => b.High);
        var low = bars.Take(5).Min(b => b.Low);
        var stop = U(rising ? low : high);
        var extreme = U(rising ? high : low);
        var stepUnits = U(step);
        var maximumUnits = U(maximum);
        var factor = stepUnits;
        var priorHigh = U(bars[4].High);
        var priorLow = U(bars[4].Low);
        var olderHigh = U(bars[3].High);
        var olderLow = U(bars[3].Low);
        result[4] = Publish(stop);
        for (var i = 5; i < bars.Count; i++)
        {
            var currentHigh = U(bars[i].High);
            var currentLow = U(bars[i].Low);
            var unchanged = rising ? priorLow > stop : priorHigh < stop;
            if (!unchanged)
            {
                stop = extreme;
                rising = !rising;
                extreme = rising
                    ? ParabolicStopValue.Max(extreme, currentHigh)
                    : ParabolicStopValue.Min(extreme, currentLow);
                factor = stepUnits;
            }
            else
            {
                stop = Advance(stop, extreme, factor);
                stop = rising
                    ? ParabolicStopValue.Min(stop, ParabolicStopValue.Min(priorLow, olderLow))
                    : ParabolicStopValue.Max(stop, ParabolicStopValue.Max(priorHigh, olderHigh));
                var next = rising ? currentHigh : currentLow;
                var fresh = rising ? next > extreme : next < extreme;
                extreme = rising ? ParabolicStopValue.Max(extreme, next) : ParabolicStopValue.Min(extreme, next);
                if (fresh && (rising ? currentLow > stop : currentHigh < stop))
                    factor = Increase(factor, stepUnits, maximumUnits);
            }
            result[i] = Publish(stop);
            olderHigh = priorHigh;
            olderLow = priorLow;
            priorHigh = currentHigh;
            priorLow = currentLow;
        }
        return result;
    }
}

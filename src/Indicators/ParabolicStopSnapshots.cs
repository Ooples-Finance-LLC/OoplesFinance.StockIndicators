using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators.Fast;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Nullable stop and reversal flag after first-trend confirmation.</summary>
public sealed record ConfirmedParabolicValue(double? Sar, bool? IsReversal);

/// <summary>Parabolic stop conventions with exact comparisons and wide once-rounded updates.</summary>
public static class ParabolicStopSnapshots
{
    private static readonly BigInteger Grid = BigInteger.One << 1074;

    private static BigInteger U(double v) => ExactVarianceWindow.Units(v);

    private static BigInteger Advance(BigInteger stop, BigInteger extreme, BigInteger factor) =>
        RocBankValue.RoundUnits(stop * Grid + factor * (extreme - stop), Grid);

    private static BigInteger Increase(BigInteger factor, BigInteger step, BigInteger maximum) =>
        RocBankValue.RoundUnits(BigInteger.Min(factor + step, maximum), 1);

    // Whether a + b exceeds c + d, decided without rounding either sum.
    internal static bool SumIsGreater(double a, double b, double c, double d) => U(a) + U(b) > U(c) + U(d);

    private static double Publish(BigInteger value)
    {
        var result = ExactMeanAccumulator.UnitRatio(value, 1);
        return FrameworkCompatibility.IsFinite(result)
            ? result
            : throw new OverflowException("Parabolic stop is not representable.");
    }

    private static void Validate(IReadOnlyList<Bar> bars, params double[] parameters)
    {
        if (bars is null) throw new ArgumentNullException(nameof(bars));
        if (parameters.Any(v => !FrameworkCompatibility.IsFinite(v)))
            throw new ArgumentOutOfRangeException(nameof(parameters));
        // An array or a list is read in place: asking the interface for every bar costs more than the stop.
        if (bars is Bar[] array)
        {
            ValidateRows(array);
            return;
        }
#if NET5_0_OR_GREATER
        if (bars is List<Bar> list)
        {
            ValidateRows(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(list));
            return;
        }
#endif
        foreach (var b in bars)
            if (!FrameworkCompatibility.IsFinite(b.High) || !FrameworkCompatibility.IsFinite(b.Low))
                throw new ArgumentOutOfRangeException(nameof(bars));
    }

    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    private static void ValidateRows(ReadOnlySpan<Bar> rows)
    {
        // value - value is +0 for a finite value and NaN otherwise, so the differences' bits stay zero only
        // while every high and low is finite.
        long bits = 0;
        for (var i = 0; i < rows.Length; i++)
        {
            double high = rows[i].High, low = rows[i].Low;
            bits |= BitConverter.DoubleToInt64Bits(high - high) | BitConverter.DoubleToInt64Bits(low - low);
        }
        if (bits != 0) throw new ArgumentOutOfRangeException("bars");
    }

    /// <summary>Unsigned stop with directional-movement initialization and nonnegative acceleration/maximum.</summary>
    public static IReadOnlyList<double?> Classic(
        IReadOnlyList<Bar> bars,
        double acceleration = .02,
        double maximum = .2,
        IndicatorNumerics numerics = IndicatorNumerics.Exact
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
            maximum,
            numerics
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
        double maximumShort = .2,
        IndicatorNumerics numerics = IndicatorNumerics.Exact
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
            maximumShort,
            numerics
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
        double maximumShort,
        IndicatorNumerics numerics
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
        if (numerics == IndicatorNumerics.Fast)
            return FastSnapshots.ParabolicClassic(
                bars,
                signed,
                start,
                offset,
                initialLong,
                stepLong,
                maximumLong,
                initialShort,
                stepShort,
                maximumShort
            );
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
        var rising = start == 0 ? !(down > 0 && down > upMove) : start > 0;
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
                    ? BigInteger.Min(extreme, BigInteger.Min(priorLow, low))
                    : BigInteger.Max(extreme, BigInteger.Max(priorHigh, high));
                stop = RocBankValue.RoundUnits(
                    stop * (Grid + (rising ? -offsetUnits : offsetUnits)),
                    Grid
                );
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
                ? BigInteger.Min(stop, BigInteger.Min(priorLow, low))
                : BigInteger.Max(stop, BigInteger.Max(priorHigh, high));
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
        double? initialFactor = null,
        IndicatorNumerics numerics = IndicatorNumerics.Exact
    )
    {
        var initial = initialFactor ?? step;
        Validate(bars, step, maximum, initial);
        if (step <= 0 || maximum < step || initial <= 0 || initial > maximum)
            throw new ArgumentOutOfRangeException(nameof(step));
        if (numerics == IndicatorNumerics.Fast)
            return FastSnapshots.ParabolicConfirmed(bars, step, maximum, initial);
        var result = new ConfirmedParabolicValue[bars.Count];
        if (bars.Count == 0)
            return result;
        var stops = new BigInteger[bars.Count];
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
                    ? BigInteger.Min(stop, BigInteger.Min(priorLow, olderLow))
                    : BigInteger.Max(stop, BigInteger.Max(priorHigh, olderHigh));
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
        double maximum = .2,
        IndicatorNumerics numerics = IndicatorNumerics.Exact
    )
    {
        Validate(bars, step, maximum);
        if (step > maximum)
            throw new ArgumentOutOfRangeException(nameof(step));
        if (numerics == IndicatorNumerics.Fast)
            return FastSnapshots.ParabolicFourBar(bars, step, maximum);
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
                    ? BigInteger.Max(extreme, currentHigh)
                    : BigInteger.Min(extreme, currentLow);
                factor = stepUnits;
            }
            else
            {
                stop = Advance(stop, extreme, factor);
                stop = rising
                    ? BigInteger.Min(stop, BigInteger.Min(priorLow, olderLow))
                    : BigInteger.Max(stop, BigInteger.Max(priorHigh, olderHigh));
                var next = rising ? currentHigh : currentLow;
                var fresh = rising ? next > extreme : next < extreme;
                extreme = rising ? BigInteger.Max(extreme, next) : BigInteger.Min(extreme, next);
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

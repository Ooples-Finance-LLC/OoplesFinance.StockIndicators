using System.Numerics;
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
        for (var i = 1; i < bars.Count; i++)
        {
            var high = U(bars[i].High);
            var low = U(bars[i].Low);
            var priorHigh = U(bars[Math.Max(1, i - 1)].High);
            var priorLow = U(bars[Math.Max(1, i - 1)].Low);
            var reversal = rising ? low <= stop : high >= stop;
            if (reversal)
            {
                rising = !rising;
                stop = rising
                    ? BigInteger.Min(extreme, BigInteger.Min(priorLow, low))
                    : BigInteger.Max(extreme, BigInteger.Max(priorHigh, high));
                stop = RocBankValue.RoundUnits(
                    stop * (Grid + (rising ? -U(offset) : U(offset))),
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
        var result = Enumerable
            .Range(0, bars.Count)
            .Select(_ => new ConfirmedParabolicValue(null, null))
            .ToArray();
        if (bars.Count == 0)
            return result;
        var stops = new BigInteger[bars.Count];
        var reversals = new bool[bars.Count];
        var rising = true;
        var extreme = U(bars[0].High);
        var stop = U(bars[0].Low);
        var factor = U(initial);
        var first = -1;
        for (var i = 1; i < bars.Count; i++)
        {
            stop = Advance(stop, extreme, factor);
            if (i >= 2)
                stop = rising
                    ? BigInteger.Min(stop, BigInteger.Min(U(bars[i - 1].Low), U(bars[i - 2].Low)))
                    : BigInteger.Max(
                        stop,
                        BigInteger.Max(U(bars[i - 1].High), U(bars[i - 2].High))
                    );
            var reversal = rising ? U(bars[i].Low) < stop : U(bars[i].High) > stop;
            if (reversal)
            {
                stop = extreme;
                rising = !rising;
                extreme = U(rising ? bars[i].High : bars[i].Low);
                factor = U(initial);
                if (first < 0)
                    first = i;
            }
            else if (rising ? U(bars[i].High) > extreme : U(bars[i].Low) < extreme)
            {
                extreme = U(rising ? bars[i].High : bars[i].Low);
                factor = Increase(factor, U(step), U(maximum));
            }
            stops[i] = stop;
            reversals[i] = reversal;
        }
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
        var factor = U(step);
        result[4] = Publish(stop);
        for (var i = 5; i < bars.Count; i++)
        {
            var unchanged = rising ? U(bars[i - 1].Low) > stop : U(bars[i - 1].High) < stop;
            if (!unchanged)
            {
                stop = extreme;
                rising = !rising;
                extreme = rising
                    ? BigInteger.Max(extreme, U(bars[i].High))
                    : BigInteger.Min(extreme, U(bars[i].Low));
                factor = U(step);
            }
            else
            {
                stop = Advance(stop, extreme, factor);
                stop = rising
                    ? BigInteger.Min(stop, BigInteger.Min(U(bars[i - 1].Low), U(bars[i - 2].Low)))
                    : BigInteger.Max(
                        stop,
                        BigInteger.Max(U(bars[i - 1].High), U(bars[i - 2].High))
                    );
                var next = U(rising ? bars[i].High : bars[i].Low);
                var fresh = rising ? next > extreme : next < extreme;
                extreme = rising ? BigInteger.Max(extreme, next) : BigInteger.Min(extreme, next);
                if (fresh && (rising ? U(bars[i].Low) > stop : U(bars[i].High) < stop))
                    factor = Increase(factor, U(step), U(maximum));
            }
            result[i] = Publish(stop);
        }
        return result;
    }
}

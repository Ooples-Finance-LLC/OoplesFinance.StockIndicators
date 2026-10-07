using System.Globalization;
using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Pivot point formula.</summary>
public enum PivotLevelStyle
{
    /// <summary>Standard floor pivots.</summary>
    Standard,

    /// <summary>Camarilla levels based on close.</summary>
    Camarilla,

    /// <summary>DeMark levels selected by open/close direction.</summary>
    Demark,

    /// <summary>Fibonacci range offsets.</summary>
    Fibonacci,

    /// <summary>Woodie pivots using the current window's open.</summary>
    Woodie,
}

/// <summary>Calendar partition for pivot levels.</summary>
public enum PivotCalendarWindow
{
    /// <summary>Complete date and hour.</summary>
    Hour,

    /// <summary>Complete calendar date.</summary>
    Day,

    /// <summary>Invariant-culture week number within its calendar year.</summary>
    Week,

    /// <summary>Complete calendar year and month.</summary>
    Month,
}

/// <summary>Pivot, four support levels and four resistance levels; unused formula levels are absent.</summary>
public sealed record PivotLevelValue(
    double? PP,
    double? S1,
    double? S2,
    double? S3,
    double? S4,
    double? R1,
    double? R2,
    double? R3,
    double? R4
);

/// <summary>Pivot snapshots from previous rolling or completed calendar windows.</summary>
/// <remarks>Input order is preserved. Formula constants are exact decimal rationals and
/// each complete published level rounds once. Intermediate range or pivot overflow is
/// harmless; unrepresentable final levels are rejected. Calendar identities include the
/// year and date components, so sparse data cannot combine distinct windows.</remarks>
public static class PivotLevelSnapshots
{
    /// <summary>Calculates levels from a preceding positive window, skipping an optional nonnegative offset.</summary>
    public static IReadOnlyList<PivotLevelValue> Rolling(
        IReadOnlyList<Bar> bars,
        int period = 20,
        int offset = 0,
        PivotLevelStyle style = PivotLevelStyle.Standard
    )
    {
        Validate(bars, style);
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset));
        var result = new PivotLevelValue[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            if (i < (long)period + offset)
            {
                result[i] = Empty();
                continue;
            }
            var start = i - period - offset;
            var end = i - offset - 1;
            var high = bars[start].High;
            var low = bars[start].Low;
            for (var j = start + 1; j <= end; j++)
            {
                high = Math.Max(high, bars[j].High);
                low = Math.Min(low, bars[j].Low);
            }
            result[i] = Levels(bars[i].Open, high, low, bars[end].Close, style);
        }
        return result;
    }

    /// <summary>Calculates levels from the previous observed calendar window; the first window is absent.</summary>
    public static IReadOnlyList<PivotLevelValue> Calendar(
        IReadOnlyList<Bar> bars,
        PivotCalendarWindow window = PivotCalendarWindow.Day,
        PivotLevelStyle style = PivotLevelStyle.Standard
    )
    {
        Validate(bars, style);
        if (window is < PivotCalendarWindow.Hour or > PivotCalendarWindow.Month)
            throw new ArgumentOutOfRangeException(nameof(window));
        if (bars.Count == 0)
            return Array.Empty<PivotLevelValue>();
        var result = new PivotLevelValue[bars.Count];
        var key = Key(bars[0].Time, window);
        var open = bars[0].Open;
        var high = bars[0].High;
        var low = bars[0].Low;
        var close = bars[0].Close;
        var current = Empty();
        for (var i = 0; i < bars.Count; i++)
        {
            var bar = bars[i];
            var next = Key(bar.Time, window);
            if (next != key)
            {
                current = Levels(
                    style == PivotLevelStyle.Woodie ? bar.Open : open,
                    high,
                    low,
                    close,
                    style
                );
                key = next;
                open = bar.Open;
                high = bar.High;
                low = bar.Low;
            }
            result[i] = current;
            high = Math.Max(high, bar.High);
            low = Math.Min(low, bar.Low);
            close = bar.Close;
        }
        return result;
    }

    private static long Key(DateTime time, PivotCalendarWindow window) =>
        window switch
        {
            PivotCalendarWindow.Hour => time.Ticks / TimeSpan.TicksPerHour,
            PivotCalendarWindow.Day => time.Ticks / TimeSpan.TicksPerDay,
            PivotCalendarWindow.Month => (long)time.Year * 12 + time.Month,
            _ => (long)time.Year * 100
                + CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
                    time,
                    CultureInfo.InvariantCulture.DateTimeFormat.CalendarWeekRule,
                    CultureInfo.InvariantCulture.DateTimeFormat.FirstDayOfWeek
                ),
        };

    private static void Validate(IReadOnlyList<Bar> bars, PivotLevelStyle style)
    {
        ArgumentNullException.ThrowIfNull(bars);
        if (style is < PivotLevelStyle.Standard or > PivotLevelStyle.Woodie)
            throw new ArgumentOutOfRangeException(nameof(style));
        foreach (var b in bars)
            if (
                !double.IsFinite(b.Open)
                || !double.IsFinite(b.High)
                || !double.IsFinite(b.Low)
                || !double.IsFinite(b.Close)
            )
                throw new ArgumentOutOfRangeException(nameof(bars));
    }

    private static PivotLevelValue Empty() =>
        new(null, null, null, null, null, null, null, null, null);

    private static PivotLevelValue Levels(
        double open,
        double high,
        double low,
        double close,
        PivotLevelStyle style
    )
    {
        var o = ExactVarianceWindow.Units(open);
        var h = ExactVarianceWindow.Units(high);
        var l = ExactVarianceWindow.Units(low);
        var c = ExactVarianceWindow.Units(close);
        var range = h - l;
        var values = new double?[9];
        double Q(BigInteger n, BigInteger d)
        {
            var value = ExactMeanAccumulator.UnitRatio(n, d);
            return double.IsFinite(value)
                ? value
                : throw new OverflowException("Pivot level is not representable.");
        }
        if (style == PivotLevelStyle.Camarilla)
        {
            values[0] = close;
            var divisors = new[] { 120, 60, 40, 20 };
            for (var i = 0; i < 4; i++)
            {
                values[i + 1] = Q(c * divisors[i] - 11 * range, divisors[i]);
                values[i + 5] = Q(c * divisors[i] + 11 * range, divisors[i]);
            }
        }
        else if (style == PivotLevelStyle.Demark)
        {
            var x =
                c < o ? h + 2 * l + c
                : c > o ? 2 * h + l + c
                : h + l + 2 * c;
            values[0] = Q(x, 4);
            values[1] = Q(x - 2 * h, 2);
            values[5] = Q(x - 2 * l, 2);
        }
        else
        {
            var numerator = style == PivotLevelStyle.Woodie ? h + l + 2 * o : h + l + c;
            var denominator = style == PivotLevelStyle.Woodie ? 4 : 3;
            values[0] = Q(numerator, denominator);
            if (style == PivotLevelStyle.Fibonacci)
            {
                var factors = new[] { 382, 618, 1000 };
                for (var i = 0; i < 3; i++)
                {
                    values[i + 1] = Q(
                        1000 * numerator - factors[i] * range * denominator,
                        1000 * denominator
                    );
                    values[i + 5] = Q(
                        1000 * numerator + factors[i] * range * denominator,
                        1000 * denominator
                    );
                }
            }
            else
            {
                values[1] = Q(2 * numerator - h * denominator, denominator);
                values[5] = Q(2 * numerator - l * denominator, denominator);
                values[2] = Q(numerator - range * denominator, denominator);
                values[6] = Q(numerator + range * denominator, denominator);
                values[3] = Q(2 * numerator + (l - 2 * h) * denominator, denominator);
                values[7] = Q(2 * numerator + (h - 2 * l) * denominator, denominator);
            }
        }
        return new(
            values[0],
            values[1],
            values[2],
            values[3],
            values[4],
            values[5],
            values[6],
            values[7],
            values[8]
        );
    }
}

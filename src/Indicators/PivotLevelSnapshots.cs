using System.Globalization;
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
        var capacity = Math.Min(period, bars.Count);
        var highs = new ExtremeDeque(capacity, true);
        var lows = new ExtremeDeque(capacity, false);
        for (var i = 0; i < bars.Count; i++)
        {
            var end = i - offset - 1;
            if (end >= 0)
            {
                highs.Add(end, bars[end].High, (long)end - period + 1);
                lows.Add(end, bars[end].Low, (long)end - period + 1);
            }
            if (i < (long)period + offset)
            {
                result[i] = Empty();
                continue;
            }
            result[i] = Levels(bars[i].Open, highs.Value, lows.Value, bars[end].Close, style);
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
        if (bars is null) throw new ArgumentNullException(nameof(bars));
        if (style is < PivotLevelStyle.Standard or > PivotLevelStyle.Woodie)
            throw new ArgumentOutOfRangeException(nameof(style));
        foreach (var b in bars)
            if (
                !FrameworkCompatibility.IsFinite(b.Open)
                || !FrameworkCompatibility.IsFinite(b.High)
                || !FrameworkCompatibility.IsFinite(b.Low)
                || !FrameworkCompatibility.IsFinite(b.Close)
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
        Span<double> values = stackalloc double[9];
        FillLevels(open, high, low, close, style, values);
        static double? Optional(double value) => double.IsNaN(value) ? null : value;
        return new(Optional(values[0]), Optional(values[1]), Optional(values[2]),
            Optional(values[3]), Optional(values[4]), Optional(values[5]),
            Optional(values[6]), Optional(values[7]), Optional(values[8]));
    }

    internal static void FillLevels(double open, double high, double low, double close,
        PivotLevelStyle style, Span<double> values)
    {
        values.Fill(double.NaN);
        double Q(int ow, int hw, int lw, int cw, int denominator)
        {
            var sum = new ExactMeanAccumulator();
            sum.Add(open, ow); sum.Add(high, hw); sum.Add(low, lw); sum.Add(close, cw);
            var value = sum.Mean(denominator);
            return FrameworkCompatibility.IsFinite(value)
                ? value : throw new OverflowException("Pivot level is not representable.");
        }
        if (style == PivotLevelStyle.Camarilla)
        {
            values[0] = close;
            for (var i = 0; i < 4; i++)
            {
                var d = i == 0 ? 120 : i == 1 ? 60 : i == 2 ? 40 : 20;
                values[i + 1] = Q(0, -11, 11, d, d);
                values[i + 5] = Q(0, 11, -11, d, d);
            }
        }
        else if (style == PivotLevelStyle.Demark)
        {
            var hw = close > open ? 2 : 1;
            var lw = close < open ? 2 : 1;
            var cw = close == open ? 2 : 1; // NOSONAR: DeMark selects exact price ties.
            values[0] = Q(0, hw, lw, cw, 4);
            values[1] = Q(0, hw - 2, lw, cw, 2);
            values[5] = Q(0, hw, lw - 2, cw, 2);
        }
        else
        {
            var ow = style == PivotLevelStyle.Woodie ? 2 : 0;
            var cw = style == PivotLevelStyle.Woodie ? 0 : 1;
            var d = style == PivotLevelStyle.Woodie ? 4 : 3;
            values[0] = Q(ow, 1, 1, cw, d);
            if (style == PivotLevelStyle.Fibonacci)
            {
                for (var i = 0; i < 3; i++)
                {
                    var f = i == 0 ? 382 : i == 1 ? 618 : 1000;
                    values[i + 1] = Q(0, 1000 - f * d, 1000 + f * d, 1000, 1000 * d);
                    values[i + 5] = Q(0, 1000 + f * d, 1000 - f * d, 1000, 1000 * d);
                }
            }
            else
            {
                values[1] = Q(2 * ow, 2 - d, 2, 2 * cw, d);
                values[5] = Q(2 * ow, 2, 2 - d, 2 * cw, d);
                values[2] = Q(ow, 1 - d, 1 + d, cw, d);
                values[6] = Q(ow, 1 + d, 1 - d, cw, d);
                values[3] = Q(2 * ow, 2 - 2 * d, 2 + d, 2 * cw, d);
                values[7] = Q(2 * ow, 2 + d, 2 - 2 * d, 2 * cw, d);
            }
        }
    }
}

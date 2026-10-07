using System.Globalization;
using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class PivotLevelComparison
{
    internal static readonly string[] Names =
    [
        "PP",
        "S1",
        "S2",
        "S3",
        "S4",
        "R1",
        "R2",
        "R3",
        "R4",
    ];

    internal static ComparisonPair Pair(
        bool calendar,
        PivotLevelStyle style = PivotLevelStyle.Standard,
        int offset = 0,
        PivotCalendarWindow window = PivotCalendarWindow.Hour
    ) =>
        new(
            calendar ? "Skender.GetPivotPoints" : "Skender.GetRollingPivots",
            nameof(PivotLevelSnapshots),
            (d, p) => Native(d, p, offset, style, calendar, window),
            (d, p) => Owned(d.IndicatorBars, p, offset, style, calendar, window),
            (d, p) => Series(Reference(d, p, offset, style, calendar, window, false)),
            Names,
            MinimumInputCount: 0,
            CompetitorReference: (d, p) =>
                Series(Reference(d, p, offset, style, calendar, window, true)),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static ComparisonSeries Series(double?[][] rows) =>
        RetrospectivePriceComparison.Series(Names, rows);

    internal static PeriodSize NativeWindow(PivotCalendarWindow window) =>
        window switch
        {
            PivotCalendarWindow.Hour => PeriodSize.OneHour,
            PivotCalendarWindow.Day => PeriodSize.Day,
            PivotCalendarWindow.Week => PeriodSize.Week,
            _ => PeriodSize.Month,
        };

    internal static ComparisonSeries Native(
        CompetitorData data,
        int period,
        int offset,
        PivotLevelStyle style,
        bool calendar,
        PivotCalendarWindow window
    )
    {
        if (calendar)
        {
            var rows = data
                .Quotes.GetPivotPoints(NativeWindow(window), (PivotPointType)style)
                .ToArray();
            return Series([
                rows.Select(r => (double?)r.PP).ToArray(),
                rows.Select(r => (double?)r.S1).ToArray(),
                rows.Select(r => (double?)r.S2).ToArray(),
                rows.Select(r => (double?)r.S3).ToArray(),
                rows.Select(r => (double?)r.S4).ToArray(),
                rows.Select(r => (double?)r.R1).ToArray(),
                rows.Select(r => (double?)r.R2).ToArray(),
                rows.Select(r => (double?)r.R3).ToArray(),
                rows.Select(r => (double?)r.R4).ToArray(),
            ]);
        }
        var a = data.Quotes.GetRollingPivots(period, offset, (PivotPointType)style).ToArray();
        return Series([
            a.Select(r => (double?)r.PP).ToArray(),
            a.Select(r => (double?)r.S1).ToArray(),
            a.Select(r => (double?)r.S2).ToArray(),
            a.Select(r => (double?)r.S3).ToArray(),
            a.Select(r => (double?)r.S4).ToArray(),
            a.Select(r => (double?)r.R1).ToArray(),
            a.Select(r => (double?)r.R2).ToArray(),
            a.Select(r => (double?)r.R3).ToArray(),
            a.Select(r => (double?)r.R4).ToArray(),
        ]);
    }

    internal static ComparisonSeries Owned(
        Bar[] bars,
        int period,
        int offset,
        PivotLevelStyle style,
        bool calendar,
        PivotCalendarWindow window
    )
    {
        var rows = calendar
            ? PivotLevelSnapshots.Calendar(bars, window, style)
            : PivotLevelSnapshots.Rolling(bars, period, offset, style);
        return Series([
            rows.Select(r => r.PP).ToArray(),
            rows.Select(r => r.S1).ToArray(),
            rows.Select(r => r.S2).ToArray(),
            rows.Select(r => r.S3).ToArray(),
            rows.Select(r => r.S4).ToArray(),
            rows.Select(r => r.R1).ToArray(),
            rows.Select(r => r.R2).ToArray(),
            rows.Select(r => r.R3).ToArray(),
            rows.Select(r => r.R4).ToArray(),
        ]);
    }

    internal static string WindowKey(DateTime date, PivotCalendarWindow window, bool native)
    {
        var culture = CultureInfo.InvariantCulture;
        var number = window switch
        {
            PivotCalendarWindow.Hour => date.Hour,
            PivotCalendarWindow.Day => date.Day,
            PivotCalendarWindow.Month => date.Month,
            _ => culture.Calendar.GetWeekOfYear(
                date,
                culture.DateTimeFormat.CalendarWeekRule,
                culture.DateTimeFormat.FirstDayOfWeek
            ),
        };
        if (native)
            return number.ToString(culture);
        return window switch
        {
            PivotCalendarWindow.Hour => date.ToString("yyyy-MM-dd-HH", culture),
            PivotCalendarWindow.Day => date.ToString("yyyy-MM-dd", culture),
            PivotCalendarWindow.Month => date.ToString("yyyy-MM", culture),
            _ => date.Year.ToString(culture) + "/" + number.ToString(culture),
        };
    }

    internal static double?[][] Reference(
        CompetitorData data,
        int period,
        int offset,
        PivotLevelStyle style,
        bool calendar,
        PivotCalendarWindow window,
        bool native
    )
    {
        var rows = Enumerable.Range(0, 9).Select(_ => new double?[data.Count]).ToArray();
        var currentStart = 0;
        var previousStart = 0;
        var previousEnd = -1;
        for (var i = 0; i < data.Count; i++)
        {
            int start,
                end,
                openIndex;
            if (calendar)
            {
                if (
                    i > 0
                    && WindowKey(data.Dates[i], window, native)
                        != WindowKey(data.Dates[i - 1], window, native)
                )
                {
                    previousStart = currentStart;
                    previousEnd = i - 1;
                    currentStart = i;
                }
                if (previousEnd < 0)
                    continue;
                start = previousStart;
                end = previousEnd;
                openIndex = style == PivotLevelStyle.Woodie ? currentStart : previousStart;
            }
            else
            {
                if (i < (long)period + offset)
                    continue;
                start = i - period - offset;
                end = i - offset - 1;
                openIndex = i;
            }
            double?[] values;
            if (native)
            {
                var quotes = data.Quotes.Skip(start).Take(end - start + 1).ToArray();
                values = DecimalLevels(
                    data.Quotes[openIndex].Open,
                    quotes.Max(q => q.High),
                    quotes.Min(q => q.Low),
                    data.Quotes[end].Close,
                    style
                );
            }
            else
            {
                var bars = data.IndicatorBars.Skip(start).Take(end - start + 1).ToArray();
                values = ExactLevels(
                    data.Opens[openIndex],
                    bars.Max(b => b.High),
                    bars.Min(b => b.Low),
                    data.Closes[end],
                    style
                );
            }
            for (var j = 0; j < 9; j++)
                rows[j][i] = values[j];
        }
        return rows;
    }

    private readonly record struct Rational(BigInteger N, BigInteger D)
    {
        internal static Rational Price(double value) => new(Units(value), Grid);

        public static implicit operator Rational(int n) => new(n, 1);

        public static Rational operator +(Rational a, Rational b) =>
            new(a.N * b.D + b.N * a.D, a.D * b.D);

        public static Rational operator -(Rational a, Rational b) =>
            new(a.N * b.D - b.N * a.D, a.D * b.D);

        public static Rational operator *(Rational a, Rational b) => new(a.N * b.N, a.D * b.D);

        public static Rational operator /(Rational a, Rational b) => new(a.N * b.D, a.D * b.N);

        internal double Value => Round(N, D);
    }

    internal static double?[] ExactLevels(
        double open,
        double high,
        double low,
        double close,
        PivotLevelStyle style
    )
    {
        var h = Rational.Price(high);
        var l = Rational.Price(low);
        var c = Rational.Price(close);
        var o = Rational.Price(open);
        var range = h - l;
        var values = new Rational?[9];
        if (style == PivotLevelStyle.Camarilla)
        {
            values[0] = c;
            var div = new[] { 12, 6, 4, 2 };
            for (var i = 0; i < 4; i++)
            {
                var width = (Rational)11 / 10 / div[i] * range;
                values[i + 1] = c - width;
                values[i + 5] = c + width;
            }
        }
        else if (style == PivotLevelStyle.Demark)
        {
            var x =
                close < open ? h + 2 * l + c
                : close > open ? 2 * h + l + c
                : h + l + 2 * c;
            values[0] = x / 4;
            values[1] = x / 2 - h;
            values[5] = x / 2 - l;
        }
        else
        {
            var pp = style == PivotLevelStyle.Woodie ? (h + l + 2 * o) / 4 : (h + l + c) / 3;
            values[0] = pp;
            if (style == PivotLevelStyle.Fibonacci)
            {
                var factors = new[] { 382, 618, 1000 };
                for (var i = 0; i < 3; i++)
                {
                    var width = (Rational)factors[i] / 1000 * range;
                    values[i + 1] = pp - width;
                    values[i + 5] = pp + width;
                }
            }
            else
            {
                values[1] = 2 * pp - h;
                values[2] = pp - range;
                values[3] = l - 2 * (h - pp);
                values[5] = 2 * pp - l;
                values[6] = pp + range;
                values[7] = h + 2 * (pp - l);
            }
        }
        return values.Select(v => v?.Value).ToArray();
    }

    private static double?[] DecimalLevels(
        decimal open,
        decimal high,
        decimal low,
        decimal close,
        PivotLevelStyle style
    )
    {
        var values = new decimal?[9];
        var range = style == PivotLevelStyle.Demark ? 0 : high - low;
        if (style == PivotLevelStyle.Camarilla)
        {
            values[0] = close;
            var div = new[] { 12, 6, 4, 2 };
            for (var i = 0; i < 4; i++)
            {
                values[i + 1] = close - 1.1m / div[i] * range;
                values[i + 5] = close + 1.1m / div[i] * range;
            }
        }
        else if (style == PivotLevelStyle.Demark)
        {
            var x =
                close < open ? high + 2 * low + close
                : close > open ? 2 * high + low + close
                : high + low + 2 * close;
            values[0] = x / 4;
            values[1] = x / 2 - high;
            values[5] = x / 2 - low;
        }
        else
        {
            var pp =
                style == PivotLevelStyle.Woodie
                    ? (high + low + 2 * open) / 4
                    : (high + low + close) / 3;
            values[0] = pp;
            if (style == PivotLevelStyle.Fibonacci)
            {
                // Preserve the native decimal scale before its decimal-to-double cast.
                var factors = new[] { .382m, .618m, 1.000m };
                for (var i = 0; i < 3; i++)
                {
                    values[i + 1] = pp - factors[i] * range;
                    values[i + 5] = pp + factors[i] * range;
                }
            }
            else
            {
                values[1] = 2 * pp - high;
                values[2] = style == PivotLevelStyle.Woodie ? pp - high + low : pp - range;
                values[3] = low - 2 * (high - pp);
                values[5] = 2 * pp - low;
                values[6] = style == PivotLevelStyle.Woodie ? pp + high - low : pp + range;
                values[7] = high + 2 * (pp - low);
            }
        }
        return values.Select(v => (double?)v).ToArray();
    }
}

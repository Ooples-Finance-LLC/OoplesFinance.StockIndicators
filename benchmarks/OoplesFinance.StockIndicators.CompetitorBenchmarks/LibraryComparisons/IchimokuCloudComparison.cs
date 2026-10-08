using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class IchimokuCloudComparison
{
    internal static readonly string[] Names =
    [
        "TenkanSen",
        "KijunSen",
        "SenkouSpanA",
        "SenkouSpanB",
        "ChikouSpan",
    ];

    internal static ComparisonPair Pair(
        int conversion = 9,
        int basis = 26,
        int spanB = 52,
        int? forward = null,
        int? backward = null
    ) =>
        new(
            "Skender.GetIchimoku",
            nameof(IchimokuCloudSnapshot),
            (d, _) => Native(d, conversion, basis, spanB, forward ?? basis, backward ?? basis),
            (d, _) =>
                Owned(
                    d.IndicatorBars,
                    conversion,
                    basis,
                    spanB,
                    forward ?? basis,
                    backward ?? basis
                ),
            (d, _) =>
                Reference(d, conversion, basis, spanB, forward ?? basis, backward ?? basis, false),
            Names,
            MinimumInputCount: 0,
            CompetitorReference: (d, _) =>
                Reference(d, conversion, basis, spanB, forward ?? basis, backward ?? basis, true),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static ComparisonSeries Series(double?[][] rows) =>
        RetrospectivePriceComparison.Series(Names, rows);

    internal static ComparisonSeries Owned(
        Bar[] bars,
        int conversion,
        int basis,
        int spanB,
        int forward,
        int backward
    )
    {
        var r = IchimokuCloudSnapshot.Calculate(bars, conversion, basis, spanB, forward, backward);
        return Series([
            r.Select(v => v.Conversion).ToArray(),
            r.Select(v => v.Base).ToArray(),
            r.Select(v => v.LeadingA).ToArray(),
            r.Select(v => v.LeadingB).ToArray(),
            r.Select(v => v.Lagging).ToArray(),
        ]);
    }

    internal static ComparisonSeries Native(
        CompetitorData d,
        int conversion,
        int basis,
        int spanB,
        int forward,
        int backward
    )
    {
        var r = d.Quotes.GetIchimoku(conversion, basis, spanB, forward, backward).ToArray();
        return NativeSeries(r);
    }

    internal static ComparisonSeries NativeSeries(IEnumerable<IchimokuResult> source)
    {
        var r = source.ToArray();
        return Series([
            r.Select(v => (double?)v.TenkanSen).ToArray(),
            r.Select(v => (double?)v.KijunSen).ToArray(),
            r.Select(v => (double?)v.SenkouSpanA).ToArray(),
            r.Select(v => (double?)v.SenkouSpanB).ToArray(),
            r.Select(v => (double?)v.ChikouSpan).ToArray(),
        ]);
    }

    // Independent window scans; native decimal intermediates remain decimal until publication.
    internal static ComparisonSeries Reference(
        CompetitorData d,
        int conversion,
        int basis,
        int spanB,
        int forward,
        int backward,
        bool native
    )
    {
        var rows = Enumerable.Range(0, 5).Select(_ => new double?[d.Count]).ToArray();
        decimal? DecimalMidpoint(int end, int period)
        {
            if (end < period - 1 || end < 0)
                return null;
            var h = d.Quotes.Skip(end - period + 1).Take(period).Max(q => q.High);
            var l = d.Quotes.Skip(end - period + 1).Take(period).Min(q => q.Low);
            return l == decimal.MaxValue ? null : (Math.Max(0, h) + l) / 2;
        }
        double? Midpoint(int end, int period)
        {
            if (end < period - 1 || end < 0)
                return null;
            return Round(
                Units(d.Highs.Skip(end - period + 1).Take(period).Max())
                    + Units(d.Lows.Skip(end - period + 1).Take(period).Min()),
                2 * Grid
            );
        }
        for (var i = 0; i < d.Count; i++)
        {
            rows[0][i] = native ? (double?)DecimalMidpoint(i, conversion) : Midpoint(i, conversion);
            rows[1][i] = native ? (double?)DecimalMidpoint(i, basis) : Midpoint(i, basis);
            var source = (long)i - forward;
            if (source >= 0)
            {
                rows[3][i] = native
                    ? (double?)DecimalMidpoint((int)source, spanB)
                    : Midpoint((int)source, spanB);
                if (i >= Math.Max(2L * forward, Math.Max(conversion, basis)) - 1)
                {
                    if (native)
                        rows[2][i] = (double?)(
                            (
                                DecimalMidpoint((int)source, conversion)
                                + DecimalMidpoint((int)source, basis)
                            ) / 2
                        );
                    else if (rows[0][(int)source] is double a && rows[1][(int)source] is double b)
                        rows[2][i] = Round(Units(a) + Units(b), 2 * Grid);
                }
            }
            if ((long)i + backward < d.Count)
                rows[4][i] = native ? (double)d.Quotes[i + backward].Close : d.Closes[i + backward];
        }
        return Series(rows);
    }
}

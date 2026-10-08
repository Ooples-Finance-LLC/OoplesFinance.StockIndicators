using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class ExtendedCloudComparison
{
    internal static readonly string[] Names =
    [
        "ConversionLine",
        "BaseLine",
        "LeadingSpanA",
        "LeadingSpanB",
        "LaggingSpan",
    ];

    internal static ComparisonSeries Series(double?[][] r) =>
        RetrospectivePriceComparison.Series(Names, r);

    internal static ComparisonPair Pair(int conversion = 9, int basis = 26, int spanB = 52) =>
        new(
            "Trady.Indicator.IchimokuCloud",
            nameof(IchimokuCloudSnapshot) + ".Extended",
            (d, _) => Native(d, conversion, basis, spanB),
            (d, _) => Owned(d.IndicatorBars, conversion, basis, spanB),
            (d, _) => Reference(d, conversion, basis, spanB, false),
            Names,
            MinimumInputCount: 0,
            CompetitorReference: (d, _) => Reference(d, conversion, basis, spanB, true),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static ComparisonSeries Owned(Bar[] bars, int conversion, int basis, int spanB)
    {
        var r = IchimokuCloudSnapshot
            .Extended(bars, conversion, basis, spanB)
            .Select(v => v.Value)
            .ToArray();
        return Series([
            r.Select(v => v.Conversion).ToArray(),
            r.Select(v => v.Base).ToArray(),
            r.Select(v => v.LeadingA).ToArray(),
            r.Select(v => v.LeadingB).ToArray(),
            r.Select(v => v.Lagging).ToArray(),
        ]);
    }

    internal static ComparisonSeries Native(CompetitorData d, int conversion, int basis, int spanB)
    {
        var r = new Trady.Analysis.Indicator.IchimokuCloud(d.Candles, conversion, basis, spanB)
            .Compute()
            .Select(v => v.Tick)
            .ToArray();
        return Series([
            r.Select(v => (double?)v.ConversionLine).ToArray(),
            r.Select(v => (double?)v.BaseLine).ToArray(),
            r.Select(v => (double?)v.LeadingSpanA).ToArray(),
            r.Select(v => (double?)v.LeadingSpanB).ToArray(),
            r.Select(v => (double?)v.LaggingSpan).ToArray(),
        ]);
    }

    internal static ComparisonSeries Reference(
        CompetitorData d,
        int conversion,
        int basis,
        int spanB,
        bool native
    )
    {
        var first = 1 - basis;
        var count = d.Count + 2 * basis - 1;
        var r = Enumerable.Range(0, 5).Select(_ => new double?[count]).ToArray();
        decimal? DecimalMid(int index, int p) =>
            index < p - 1 || index >= d.Count
                ? null
                : (
                    d.Candles.Skip(index - p + 1).Take(p).Max(v => v.High)
                    + d.Candles.Skip(index - p + 1).Take(p).Min(v => v.Low)
                ) / 2;
        double? Mid(int index, int p) =>
            index < p - 1 || index >= d.Count
                ? null
                : Round(
                    Units(d.Highs.Skip(index - p + 1).Take(p).Max())
                        + Units(d.Lows.Skip(index - p + 1).Take(p).Min()),
                    2 * Grid
                );
        for (var j = 0; j < count; j++)
        {
            var index = first + j;
            r[0][j] = native ? (double?)DecimalMid(index, conversion) : Mid(index, conversion);
            r[1][j] = native ? (double?)DecimalMid(index, basis) : Mid(index, basis);
            r[3][j] = native
                ? (double?)DecimalMid(index - basis, spanB)
                : Mid(index - basis, spanB);
            if (native)
                r[2][j] = (double?)(
                    (DecimalMid(index - basis, conversion) + DecimalMid(index - basis, basis)) / 2
                );
            else if (
                Mid(index - basis, conversion) is double a
                && Mid(index - basis, basis) is double b
            )
                r[2][j] = Round(Units(a) + Units(b), 2 * Grid);
            var lag = index + basis - 1;
            if (lag >= 0 && lag < d.Count)
                r[4][j] = native ? (double)d.Candles[lag].Close : d.Closes[lag];
        }
        return Series(r);
    }
}

using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class PivotTrendComparison
{
    internal static readonly string[] Names =
    [
        "HighPoint",
        "LowPoint",
        "HighLine",
        "LowLine",
        "HighTrend",
        "LowTrend",
    ];

    internal static ComparisonSeries Series(double?[][] rows) =>
        RetrospectivePriceComparison.Series(Names, rows);

    internal static ComparisonPair Pair(
        int left = 2,
        int right = 2,
        int max = 20,
        bool close = false
    ) =>
        new(
            "Skender.GetPivots",
            nameof(PivotTrendSnapshot),
            (d, _) =>
            {
                var r = d
                    .Quotes.GetPivots(left, right, max, close ? EndType.Close : EndType.HighLow)
                    .ToArray();
                return Series([
                    r.Select(v => (double?)v.HighPoint).ToArray(),
                    r.Select(v => (double?)v.LowPoint).ToArray(),
                    r.Select(v => (double?)v.HighLine).ToArray(),
                    r.Select(v => (double?)v.LowLine).ToArray(),
                    r.Select(v => v.HighTrend.HasValue ? (double?)(int)v.HighTrend.Value : null)
                        .ToArray(),
                    r.Select(v => v.LowTrend.HasValue ? (double?)(int)v.LowTrend.Value : null)
                        .ToArray(),
                ]);
            },
            (d, _) => Owned(d.IndicatorBars, left, right, max, close),
            (d, _) => Series(Reference(d, left, right, max, close, false)),
            Names,
            MinimumInputCount: 0,
            CompetitorReference: (d, _) => Series(Reference(d, left, right, max, close, true)),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static ComparisonSeries Owned(Bar[] bars, int left, int right, int max, bool close)
    {
        var r = PivotTrendSnapshot.Calculate(bars, left, right, max, close);
        return Series([
            r.Select(v => v.HighPoint).ToArray(),
            r.Select(v => v.LowPoint).ToArray(),
            r.Select(v => v.HighLine).ToArray(),
            r.Select(v => v.LowLine).ToArray(),
            r.Select(v => v.HighTrend.HasValue ? (double?)(int)v.HighTrend.Value : null).ToArray(),
            r.Select(v => v.LowTrend.HasValue ? (double?)(int)v.LowTrend.Value : null).ToArray(),
        ]);
    }

    internal static double?[][] Reference(
        CompetitorData data,
        int left,
        int right,
        int max,
        bool close,
        bool native
    )
    {
        var fractals = RetrospectivePriceComparison.FractalReference(
            data,
            left,
            right,
            close,
            false,
            native
        );
        var result = Enumerable.Range(0, 6).Select(_ => new double?[data.Count]).ToArray();
        for (var side = 0; side < 2; side++)
        {
            var output = fractals.Outputs[side == 0 ? "Bear" : "Bull"];
            var anchors = Enumerable.Range(0, data.Count).Where(i => output.Present![i]).ToArray();
            foreach (var i in anchors)
                result[side][i] = output.Values[i];
            for (var a = 1; a < anchors.Length; a++)
            {
                var first = anchors[a - 1];
                var last = anchors[a];
                var low = output.Values[first];
                var high = output.Values[last];
                decimal Quote(int index) =>
                    close ? data.Quotes[index].Close
                    : side == 0 ? data.Quotes[index].High
                    : data.Quotes[index].Low;
                var comparison = native ? Quote(last).CompareTo(Quote(first)) : high.CompareTo(low);
                if (last - first > max || comparison == 0)
                    continue;
                var trend = (side == 0 ? 0 : 2) + (comparison > 0 ? 0 : 1);
                for (var i = first; i <= last; i++)
                {
                    if (native)
                    {
                        var from = Quote(first);
                        var to = Quote(last);
                        result[side + 2][i] =
                            i == first
                                ? (double)from
                                : (double)(to + (to - from) / (last - first) * (i - last));
                    }
                    else
                        result[side + 2][i] = Round(
                            Units(low) * (last - i) + Units(high) * (i - first),
                            Grid * (last - first)
                        );
                    if (i > first)
                        result[side + 4][i] = trend;
                }
            }
        }
        return result;
    }
}

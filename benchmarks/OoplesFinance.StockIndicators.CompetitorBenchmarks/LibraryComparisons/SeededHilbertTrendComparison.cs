using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class SeededHilbertTrendComparison
{
    internal static readonly string[] Names = ["Trendline", "SmoothPrice", "DcPeriods"];

    internal static ComparisonPair Pair(bool midpoint = true) =>
        new(
            "Skender.GetHtTrendline",
            nameof(SeededHilbertTrendline),
            (d, _) => Native(d, midpoint),
            (d, _) => Owned(d.IndicatorBars, midpoint),
            (d, _) => Series(Reference(Input(d, midpoint, false), false)),
            Names,
            CompetitorReference: (d, _) => Series(Reference(Input(d, midpoint, true), true)),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static double[] Input(CompetitorData d, bool midpoint, bool native) =>
        midpoint ? SeededPhaseComparison.Input(d, false, native) : d.Closes;

    internal static ComparisonSeries Series(double?[][] rows) =>
        RetrospectivePriceComparison.Series(Names, rows);

    internal static ComparisonSeries Native(CompetitorData data, bool midpoint)
    {
        var r = (
            midpoint
                ? data.Quotes.GetHtTrendline()
                : data.Closes.Select((p, i) => (DateTime.UnixEpoch.AddDays(i), p)).GetHtTrendline()
        ).ToArray();
        return Series([
            r.Select(v => v.Trendline).ToArray(),
            r.Select(v => v.SmoothPrice).ToArray(),
            r.Select(v => (double?)v.DcPeriods).ToArray(),
        ]);
    }

    internal static ComparisonSeries Owned(Bar[] bars, bool midpoint)
    {
        var owner = new SeededHilbertTrendline(midpoint);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(owner)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return Series(
            Enumerable
                .Range(0, 3)
                .Select(j =>
                {
                    var values = run[owner.Outputs[j]].ToArray();
                    var flags = run[owner.Outputs[j + 3]].ToArray();
                    return values.Select((v, i) => flags[i] > 0 ? (double?)v : null).ToArray();
                })
                .ToArray()
        );
    }

    internal static double?[][] Reference(double[] p, bool native)
    {
        var periods = native
            ? SeededPhaseComparison.NativeReference(p, .5, .05, false, true)[2]
            : SeededPhaseComparison.GridReference(p, .5, .05, false, cycleReadings: true)[2];
        var result = Enumerable.Range(0, 3).Select(_ => new double?[p.Length]).ToArray();
        var means = new System.Numerics.BigInteger[p.Length];
        var nativeMeans = new double[p.Length];
        static double? Present(double v) => double.IsNaN(v) ? null : v;
        for (var i = 0; i < p.Length; i++)
        {
            result[0][i] = Present(p[i]);
            if (i < 6)
                continue;
            var count = Math.Min(
                double.IsNaN(periods[i]!.Value) ? 0 : (int)(periods[i]!.Value + .5),
                i + 1
            );
            if (count > 0)
                result[2][i] = count;
            if (native)
            {
                double sum = 0;
                for (var j = i - count + 1; j <= i; j++)
                    sum += p[j];
                nativeMeans[i] = count > 0 ? sum / count : p[i];
                result[1][i] = Present((4 * p[i] + 3 * p[i - 1] + 2 * p[i - 2] + p[i - 3]) / 10);
                if (i >= 11)
                    result[0][i] = Present(
                        (
                            4 * nativeMeans[i]
                            + 3 * nativeMeans[i - 1]
                            + 2 * nativeMeans[i - 2]
                            + nativeMeans[i - 3]
                        ) / 10
                    );
            }
            else
            {
                var sum = System.Numerics.BigInteger.Zero;
                for (var j = i - count + 1; j <= i; j++)
                    sum += Units(p[j]);
                means[i] = count > 0 ? DirectionalComparison.RoundedUnits(sum, count) : Units(p[i]);
                result[1][i] = Round(
                    4 * Units(p[i]) + 3 * Units(p[i - 1]) + 2 * Units(p[i - 2]) + Units(p[i - 3]),
                    10 * Grid
                );
                if (i >= 11)
                    result[0][i] = Round(
                        4 * means[i] + 3 * means[i - 1] + 2 * means[i - 2] + means[i - 3],
                        10 * Grid
                    );
            }
        }
        return result;
    }
}

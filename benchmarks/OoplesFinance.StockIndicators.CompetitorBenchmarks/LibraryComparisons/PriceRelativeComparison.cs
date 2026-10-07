using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class PriceRelativeComparison
{
    internal static readonly string[] Names = ["Prs", "PrsSma", "PrsPercent"];
    internal static readonly ComparisonPair Pair = Create(true, 3);

    internal static ComparisonPair Create(bool lookback, int? mean) =>
        new(
            "Skender.GetPrs",
            nameof(PriceRelativeStrength),
            (d, p) =>
                FromNative(
                    d.Dates.Zip(d.Closes, (t, v) => (t, v))
                        .GetPrs(d.Dates.Zip(d.Opens, (t, v) => (t, v)), lookback ? p : null, mean)
                ),
            (d, p) => Owned(d.IndicatorBars, lookback ? p : null, mean),
            (d, p) => Series(Reference(d.Closes, d.Opens, lookback ? p : null, mean, false)),
            Names,
            CompetitorReference: (d, p) =>
                Series(Reference(d.Closes, d.Opens, lookback ? p : null, mean, true)),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static ComparisonSeries Series(double?[][] r) =>
        RetrospectivePriceComparison.Series(Names, r);

    internal static ComparisonSeries FromNative(IEnumerable<PrsResult> values)
    {
        var r = values.ToArray();
        return Series([
            r.Select(v => v.Prs).ToArray(),
            r.Select(v => v.PrsSma).ToArray(),
            r.Select(v => v.PrsPercent).ToArray(),
        ]);
    }

    internal static ComparisonSeries Owned(
        Bar[] bars,
        int? period,
        int? mean,
        CandlePriceField evaluation = CandlePriceField.Close,
        CandlePriceField basis = CandlePriceField.Open
    )
    {
        var indicator = new PriceRelativeStrength(period, mean, evaluation, basis);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        var rows = new double?[3][];
        for (var j = 0; j < 3; j++)
        {
            var v = run[indicator.Outputs[j]].ToArray();
            var present = run[indicator.Outputs[j + 3]].ToArray();
            rows[j] = v.Select((x, i) => present[i] > 0 ? (double?)x : null).ToArray();
        }
        return Series(rows);
    }

    internal static double?[][] Reference(
        double[] e,
        double[] b,
        int? period,
        int? mean,
        bool native
    )
    {
        var r = new[] { new double?[e.Length], new double?[e.Length], new double?[e.Length] };
        for (var i = 0; i < e.Length; i++)
        {
            if (b[i] != 0) // NOSONAR: Exact zero denominator.
                r[0][i] = native ? e[i] / b[i] : Round(Units(e[i]), Units(b[i]));
            if (period.HasValue && i >= period.Value)
            {
                var j = i - period.Value;
                if (e[j] != 0 && b[j] != 0) // NOSONAR: Exact zero prior prices.
                    r[2][i] = native
                        ? (e[i] - e[j]) / e[j] - (b[i] - b[j]) / b[j]
                        : Round(
                            Units(e[i]) * Units(b[j]) - Units(b[i]) * Units(e[j]),
                            Units(e[j]) * Units(b[j])
                        );
            }
            if (mean.HasValue && i >= mean.Value - 1)
            {
                var window = r[0].Skip(i - mean.Value + 1).Take(mean.Value).ToArray();
                if (window.All(v => v.HasValue))
                    r[1][i] = native
                        ? window.Aggregate(0d, (s, v) => s + v!.Value) / mean.Value
                        : Round(
                            window.Aggregate(BigInteger.Zero, (s, v) => s + Units(v!.Value)),
                            Grid * mean.Value
                        );
            }
        }
        return r;
    }
}

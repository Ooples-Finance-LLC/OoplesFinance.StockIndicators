using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class WindowFisherComparison
{
    internal static readonly string[] Names = ["Fisher", "Trigger"];

    internal static ComparisonPair Pair(bool midpoint = true) =>
        new(
            "Skender.GetFisherTransform",
            nameof(WindowFisherTransform),
            (d, p) => Native(d, p, midpoint),
            (d, p) => Owned(d.IndicatorBars, p, midpoint),
            (d, p) =>
                Series(
                    Reference(
                        midpoint ? SeededPhaseComparison.Input(d, false, false) : d.Closes,
                        p,
                        false
                    )
                ),
            Names,
            CompetitorReference: (d, p) =>
                Series(
                    Reference(
                        midpoint ? SeededPhaseComparison.Input(d, false, true) : d.Closes,
                        p,
                        true
                    )
                ),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static ComparisonSeries Series(double?[][] rows) =>
        RetrospectivePriceComparison.Series(Names, rows);

    internal static ComparisonSeries Native(CompetitorData data, int period, bool midpoint)
    {
        var rows = (
            midpoint
                ? data.Quotes.GetFisherTransform(period)
                : data
                    .Closes.Select((v, i) => (DateTime.UnixEpoch.AddDays(i), v))
                    .GetFisherTransform(period)
        ).ToArray();
        return Series([
            rows.Select(r => r.Fisher).ToArray(),
            rows.Select(r => r.Trigger).ToArray(),
        ]);
    }

    internal static ComparisonSeries Owned(Bar[] bars, int period, bool midpoint)
    {
        var owner = new WindowFisherTransform(period, midpoint);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(owner)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return Series(
            Enumerable
                .Range(0, 2)
                .Select(j =>
                {
                    var values = run[owner.Outputs[j]].ToArray();
                    var flags = run[owner.Outputs[j + 2]].ToArray();
                    return values.Select((v, i) => flags[i] > 0 ? (double?)v : null).ToArray();
                })
                .ToArray()
        );
    }

    internal static double?[][] Reference(double[] prices, int period, bool native)
    {
        var output = Enumerable.Range(0, 2).Select(_ => new double?[prices.Length]).ToArray();
        double position = 0;
        double? fisher = 0;
        for (var i = 0; i < prices.Length; i++)
        {
            if (i == 0)
            {
                output[0][i] = 0;
                continue;
            }
            var window = prices
                .Skip(Math.Max(0, i - period + 1))
                .Take(Math.Min(i + 1, period))
                .ToArray();
            var low = window.Min();
            var high = window.Max();
            var ratio =
                high == low ? 0 // NOSONAR: S1244 - Only an exactly constant window has an undefined range.
                : native ? (prices[i] - low) / (high - low)
                : Round(Units(prices[i]) - Units(low), Units(high) - Units(low));
            position = high == low ? 0 : .33 * 2 * (ratio - .5) + .67 * position; // NOSONAR: S1244 - Preserve the native exact flat-window branch.
            if (position > .99)
                position = .999;
            if (position < -.99)
                position = -.999;
            output[1][i] = fisher;
            fisher = .5 * Math.Log((1 + position) / (1 - position)) + .5 * fisher;
            if (fisher.HasValue && double.IsNaN(fisher.Value))
                fisher = null;
            output[0][i] = fisher;
        }
        return output;
    }
}

using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class ExpandingRecurrenceComparison
{
    internal static readonly ComparisonPair[] Pairs = [Create(true), Create(false)];

    internal static ComparisonPair Create(bool regularized, double lambda = .5) =>
        new(
            regularized ? "QuanTAlib.Rema" : "QuanTAlib.Zlema",
            regularized ? nameof(ExpandingRegularizedEma) : nameof(ExpandingZeroLagEma),
            (d, p) => Native(d, p, regularized, lambda),
            (d, p) => Owned(d, p, regularized, lambda),
            (d, p) => new(0, Reference(d.Closes, p, regularized, lambda, false)),
            CompetitorReference: (d, p) =>
                new(0, Reference(d.Closes, p, regularized, lambda, true)),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    private static ComparisonSeries Native(
        CompetitorData data,
        int period,
        bool regularized,
        double lambda
    )
    {
        QuanTAlib.AbstractBase indicator = regularized
            ? new QuanTAlib.Rema(period, lambda)
            : new QuanTAlib.Zlema(period);
        return new(
            0,
            data.Closes.Select(v => indicator.Calc(new QuanTAlib.TValue(v, true, false)).Value)
                .ToArray()
        );
    }

    private static ComparisonSeries Owned(
        CompetitorData data,
        int period,
        bool regularized,
        double lambda
    )
    {
        IIndicator indicator = regularized
            ? new ExpandingRegularizedEma(period, lambda)
            : new ExpandingZeroLagEma(period);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return new(0, run[indicator.Outputs[0]].ToArray());
    }

    internal static double[] Reference(
        double[] prices,
        int period,
        bool regularized,
        double lambda,
        bool native
    )
    {
        var values = new double[prices.Length];
        for (var i = 0; i < prices.Length; i++)
        {
            if (regularized && i < 2)
            {
                values[i] = prices[i];
                continue;
            }
            var prior = i == 0 ? 0 : values[i - 1];
            var divisor = Math.Min((long)i + 1, period) + 1;
            var lagged = prices[Math.Max(0, i - (period - 1) / 2)];
            if (native)
            {
                var alpha = Divide(2, divisor);
                values[i] = regularized
                    ? Divide(
                        Add(
                            Add(prior, Multiply(alpha, Subtract(prices[i], prior))),
                            Multiply(lambda, Add(prior, Subtract(prior, values[i - 2])))
                        ),
                        Add(1, lambda)
                    )
                    : Add(
                        Multiply(Subtract(Subtract(Multiply(2, prices[i]), lagged), prior), alpha),
                        prior
                    );
            }
            else
            {
                var p = Units(prior);
                var current = Units(prices[i]);
                values[i] = regularized
                    ? Round(
                        ((divisor - 2) * p + 2 * current) * Grid
                            + divisor * Units(lambda) * (2 * p - Units(values[i - 2])),
                        divisor * Grid * (Grid + Units(lambda))
                    )
                    : Round(4 * current - 2 * Units(lagged) + (divisor - 2) * p, divisor * Grid);
            }
            if (!double.IsFinite(values[i]))
                break;
        }
        return values;
    }

    internal static CompetitorData Fixture() =>
        CompetitorData.FromCloses([1, 4, 2, 8, -3, 9, 0, 1, 3, -2, 5, 0]);
}

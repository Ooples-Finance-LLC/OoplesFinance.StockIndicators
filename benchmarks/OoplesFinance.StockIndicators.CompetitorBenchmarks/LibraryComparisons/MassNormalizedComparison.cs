using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class MassNormalizedComparison
{
    internal static readonly ComparisonPair[] Pairs = [Create([.2, .2, .2, .2])];

    // null selects the compensated integer-period constructor; a single weight
    // selects explicit-alpha EMA, four weights select QEMA.
    internal static ComparisonPair Create(double[]? alphas = null) =>
        new(
            alphas?.Length == 4 ? "QuanTAlib.Qema" : "QuanTAlib.Ema",
            alphas?.Length == 4 ? nameof(MassNormalizedQuadrupleEma) : nameof(MassNormalizedEma),
            (d, p) => Native(d, p, alphas),
            (d, p) => Owned(d.IndicatorBars, p, alphas),
            (d, p) => new(0, Reference(d.Closes, alphas ?? [2d / ((long)p + 1)], false)),
            CompetitorReference: (d, p) =>
                new(0, Reference(d.Closes, alphas ?? [2d / unchecked(p + 1)], true)),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static QuanTAlib.AbstractBase NativeIndicator(int period, double[]? alphas) =>
        alphas is null ? new QuanTAlib.Ema(period, false)
        : alphas.Length == 1 ? new QuanTAlib.Ema(alphas[0])
        : new QuanTAlib.Qema(alphas[0], alphas[1], alphas[2], alphas[3]);

    internal static IIndicator Indicator(int period, double[]? alphas) =>
        alphas is null ? new MassNormalizedEma(period)
        : alphas.Length == 1 ? new MassNormalizedEma(alphas[0])
        : new MassNormalizedQuadrupleEma(alphas[0], alphas[1], alphas[2], alphas[3]);

    private static ComparisonSeries Native(CompetitorData data, int period, double[]? alphas)
    {
        var indicator = NativeIndicator(period, alphas);
        return new(
            0,
            data.Closes.Select(v => indicator.Calc(new QuanTAlib.TValue(v, true, false)).Value)
                .ToArray()
        );
    }

    internal static ComparisonSeries Owned(Bar[] bars, int period, double[]? alphas)
    {
        var indicator = Indicator(period, alphas);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return new(0, run[indicator.Outputs[0]].ToArray());
    }

    internal static double[] Reference(double[] prices, double[] alphas, bool native)
    {
        var means = new double[alphas.Length];
        var masses = new double[alphas.Length];
        var residuals = Enumerable.Repeat(1d, alphas.Length).ToArray();
        var result = new double[prices.Length];
        for (var i = 0; i < prices.Length; i++)
        {
            var input = prices[i];
            var stages = new double[alphas.Length];
            for (var j = 0; j < alphas.Length; j++)
            {
                var a = alphas[j];
                residuals[j] = residuals[j] > 1e-10 ? Multiply(Subtract(1, a), residuals[j]) : 0;
                if (native)
                {
                    means[j] = Add(Multiply(a, Subtract(input, means[j])), means[j]);
                    input =
                        residuals[j] == 0 ? means[j] : Divide(means[j], Subtract(1, residuals[j]));
                }
                else
                {
                    var old = (Grid - Units(a)) * Units(masses[j]);
                    var added = Units(a) * Grid;
                    var weight = old + added;
                    input = Round(
                        Units(input) * added + Units(means[j]) * old,
                        Grid * (residuals[j] == 0 ? Grid * Grid : weight)
                    );
                    means[j] = input;
                    masses[j] = residuals[j] == 0 ? 1 : Round(weight, Grid * Grid);
                }
                stages[j] = input;
            }
            result[i] =
                alphas.Length == 1 ? stages[0]
                : native
                    ? Subtract(
                        Add(
                            Subtract(Multiply(4, stages[0]), Multiply(6, stages[1])),
                            Multiply(4, stages[2])
                        ),
                        stages[3]
                    )
                : Round(
                    4 * Units(stages[0])
                        - 6 * Units(stages[1])
                        + 4 * Units(stages[2])
                        - Units(stages[3]),
                    Grid
                );
        }
        return result;
    }
}

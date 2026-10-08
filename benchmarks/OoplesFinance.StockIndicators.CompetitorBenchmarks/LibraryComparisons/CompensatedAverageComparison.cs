using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class CompensatedAverageComparison
{
    internal static readonly ComparisonPair[] Pairs = [Create(2), Create(3)];

    internal static ComparisonPair Create(int order) =>
        new(
            order == 2 ? "QuanTAlib.Dema" : "QuanTAlib.Tema",
            nameof(CompensatedExponentialAverage),
            (d, p) => Native(d, p, order),
            (d, p) => Owned(d.IndicatorBars, p, order),
            (d, p) => new(0, Reference(d.Closes, p, order, false)),
            CompetitorReference: (d, p) => new(0, Reference(d.Closes, p, order, true)),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static QuanTAlib.AbstractBase NativeIndicator(int period, int order) =>
        order == 2 ? new QuanTAlib.Dema(period) : new QuanTAlib.Tema(period);

    private static ComparisonSeries Native(CompetitorData data, int period, int order)
    {
        var indicator = NativeIndicator(period, order);
        return new(
            0,
            data.Closes.Select(v => indicator.Calc(new QuanTAlib.TValue(v, true, false)).Value)
                .ToArray()
        );
    }

    internal static ComparisonSeries Owned(Bar[] bars, int period, int order)
    {
        var indicator = new CompensatedExponentialAverage(period, order);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return new(0, run[indicator.Outputs[0]].ToArray());
    }

    internal static double[] Reference(double[] prices, int period, int order, bool native)
    {
        var states = new BigInteger[order];
        var raw = new double[order];
        var output = new double[prices.Length];
        var alpha = Divide(2, native ? unchecked(period + 1) : (long)period + 1);
        var residual = 1d;
        for (var i = 0; i < prices.Length; i++)
        {
            residual = residual > 1e-10 ? Multiply(Subtract(1, alpha), residual) : 0;
            var compensation = residual > 1e-10 ? Divide(1, Subtract(1, residual)) : 1;
            if (native)
            {
                var input = prices[i];
                for (var j = 0; j < order; j++)
                {
                    raw[j] = Add(Multiply(alpha, Subtract(input, raw[j])), raw[j]);
                    input = Multiply(raw[j], compensation);
                }
                output[i] =
                    order == 2
                        ? Subtract(
                            Multiply(Multiply(2, raw[0]), compensation),
                            Multiply(raw[1], compensation)
                        )
                        : Add(
                            Subtract(
                                Multiply(Multiply(3, raw[0]), compensation),
                                Multiply(Multiply(3, raw[1]), compensation)
                            ),
                            Multiply(raw[2], compensation)
                        );
            }
            else
            {
                var input = Units(prices[i]);
                var total = BigInteger.Zero;
                for (var j = 0; j < order; j++)
                {
                    states[j] = Extended(
                        Units(alpha) * input + (Grid - Units(alpha)) * states[j],
                        Grid * Grid
                    );
                    input = Extended(states[j] * Units(compensation), Grid * Grid);
                    total +=
                        (
                            order == 2
                                ? (j == 0 ? 2 : -1)
                                : (
                                    j == 0 ? 3
                                    : j == 1 ? -3
                                    : 1
                                )
                        ) * input;
                }
                output[i] = Round(total, Grid);
            }
        }
        return output;
    }

    private static BigInteger Extended(BigInteger numerator, BigInteger denominator)
    {
        var shift = 0;
        double value;
        while (double.IsInfinity(value = Round(numerator, denominator << shift)))
            shift += 512;
        return Units(value) << shift;
    }

    internal static CompetitorData Fixture() =>
        CompetitorData.FromCloses([3, 12, -6, 9, 18, -3, 4, 4, -18, 20]);
}

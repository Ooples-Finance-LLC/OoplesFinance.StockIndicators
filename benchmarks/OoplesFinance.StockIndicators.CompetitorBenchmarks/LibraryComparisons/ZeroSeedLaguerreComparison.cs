using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class ZeroSeedLaguerreComparison
{
    internal static readonly ComparisonPair Pair = Create(.1);

    internal static ComparisonPair Create(double gamma) =>
        new(
            "QuanTAlib.Ltma",
            nameof(ZeroSeedLaguerreFilter),
            (d, _) => Native(d, gamma),
            (d, _) => Owned(d, gamma),
            (d, _) => Reference(d, gamma, false),
            CompetitorReference: (d, _) => Reference(d, gamma, true),
            ErrorBudget: OoplesFinance.StockIndicators.Validation.IndicatorErrorBudget.Exact
        );

    private static ComparisonSeries Native(CompetitorData data, double gamma)
    {
        var indicator = new QuanTAlib.Ltma(gamma);
        return new(
            0,
            data.Closes.Select(v => indicator.Calc(new QuanTAlib.TValue(v, true, false)).Value)
                .ToArray()
        );
    }

    private static ComparisonSeries Owned(CompetitorData data, double gamma)
    {
        var indicator = new ZeroSeedLaguerreFilter(gamma);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return new(0, run[indicator.Outputs[0]].ToArray());
    }

    internal static ComparisonSeries Reference(CompetitorData data, double gamma, bool native) =>
        new(0, Reference(data.Closes, gamma, native));

    internal static double[] Reference(double[] prices, double gamma, bool native)
    {
        var output = new double[prices.Length];
        var old = new BigInteger[4];
        var g = Units(gamma);
        for (var i = 0; i < prices.Length; i++)
        {
            var next = new BigInteger[4];
            if (native)
            {
                var stages = new double[4];
                stages[0] = Add(
                    Multiply(Subtract(1, gamma), prices[i]),
                    Multiply(gamma, Round(old[0], Grid))
                );
                for (var j = 1; j < 4; j++)
                    stages[j] = Add(
                        Add(Multiply(-gamma, stages[j - 1]), Round(old[j - 1], Grid)),
                        Multiply(gamma, Round(old[j], Grid))
                    );
                output[i] = Divide(
                    Add(
                        Add(Add(stages[0], Multiply(2, stages[1])), Multiply(2, stages[2])),
                        stages[3]
                    ),
                    6
                );
                for (var j = 0; j < 4; j++)
                    next[j] = Units(stages[j]);
            }
            else
            {
                next[0] = ExtendedUnits((Grid - g) * Units(prices[i]) + g * old[0], Grid * Grid);
                for (var j = 1; j < 4; j++)
                    next[j] = ExtendedUnits(
                        old[j - 1] * Grid + g * (old[j] - next[j - 1]),
                        Grid * Grid
                    );
                output[i] = Round(next[0] + 2 * next[1] + 2 * next[2] + next[3], 6 * Grid);
            }
            old = next;
        }
        return output;
    }

    private static BigInteger ExtendedUnits(BigInteger numerator, BigInteger denominator)
    {
        for (var shift = 0; ; shift += 512)
        {
            var rounded = Round(numerator, denominator << shift);
            if (double.IsFinite(rounded))
                return Units(rounded) << shift;
        }
    }

    internal static CompetitorData Fixture() =>
        CompetitorData.FromCloses([6, 0, 0, 0, 0, 12, -6, 3, 0, 0, 0, 0]);
}

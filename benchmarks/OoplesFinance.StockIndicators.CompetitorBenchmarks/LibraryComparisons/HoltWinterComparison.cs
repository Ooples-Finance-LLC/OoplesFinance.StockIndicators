using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class HoltWinterComparison
{
    internal static readonly ComparisonPair Pair = Create();

    internal static ComparisonPair Create(double[]? factors = null, bool derivePeriod = false) =>
        new(
            "QuanTAlib.Hwma",
            nameof(HoltWinterForecast),
            (d, p) => Native(d, p, factors, derivePeriod),
            (d, p) => Owned(d, p, factors, derivePeriod),
            (d, p) =>
                new(0, Reference(d.Closes, ActualPeriod(p, factors, derivePeriod), factors, false)),
            CompetitorReference: (d, p) =>
                new(0, Reference(d.Closes, ActualPeriod(p, factors, derivePeriod), factors, true)),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    private static int ActualPeriod(int period, double[]? factors, bool derived) =>
        derived ? (int)((2 - factors![0]) / factors[0]) : period;

    private static ComparisonSeries Native(
        CompetitorData data,
        int period,
        double[]? factors,
        bool derived
    )
    {
        var indicator =
            factors is null ? new QuanTAlib.Hwma(period)
            : derived ? new QuanTAlib.Hwma(factors[0], factors[1], factors[2])
            : new QuanTAlib.Hwma(period, factors[0], factors[1], factors[2]);
        return new(
            0,
            data.Closes.Select(v => indicator.Calc(new QuanTAlib.TValue(v, true, false)).Value)
                .ToArray()
        );
    }

    private static ComparisonSeries Owned(
        CompetitorData data,
        int period,
        double[]? factors,
        bool derived
    )
    {
        var indicator =
            factors is null ? new HoltWinterForecast(period)
            : derived ? new HoltWinterForecast(factors[0], factors[1], factors[2])
            : new HoltWinterForecast(period, factors[0], factors[1], factors[2]);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return new(0, run[indicator.Outputs[0]].ToArray());
    }

    internal static double[] Reference(double[] prices, int period, double[]? factors, bool native)
    {
        var parameters =
            factors
            ??
            [
                Divide(2, native ? unchecked(period + 1) : (long)period + 1),
                Divide(1, period),
                Divide(1, period),
            ];
        if (period == 1)
            parameters = [1, 0, 0];
        var a = Units(parameters[0]);
        var b = Units(parameters[1]);
        var c = Units(parameters[2]);
        BigInteger level = 0,
            velocity = 0,
            acceleration = 0;
        var values = new double[prices.Length];
        for (var i = 0; i < prices.Length; i++)
        {
            if (i == 0)
                level = Units(prices[i]);
            if (native)
            {
                var f0 = Round(level, Grid);
                var v0 = Round(velocity, Grid);
                var a0 = Round(acceleration, Grid);
                var f = Add(
                    Multiply(Subtract(1, parameters[0]), Add(Add(f0, v0), Multiply(.5, a0))),
                    Multiply(parameters[0], prices[i])
                );
                var v = Add(
                    Multiply(Subtract(1, parameters[1]), Add(v0, a0)),
                    Multiply(parameters[1], Subtract(f, f0))
                );
                var acc = Add(
                    Multiply(Subtract(1, parameters[2]), a0),
                    Multiply(parameters[2], Subtract(v, v0))
                );
                values[i] = Add(Add(f, v), Multiply(.5, acc));
                level = Units(f);
                velocity = Units(v);
                acceleration = Units(acc);
            }
            else
            {
                var f = ExtendedUnits(
                    (Grid - a) * (2 * level + 2 * velocity + acceleration)
                        + 2 * a * Units(prices[i]),
                    2 * Grid * Grid
                );
                var v = ExtendedUnits(
                    (Grid - b) * (velocity + acceleration) + b * (f - level),
                    Grid * Grid
                );
                var acc = ExtendedUnits(
                    (Grid - c) * acceleration + c * (v - velocity),
                    Grid * Grid
                );
                values[i] = Round(2 * f + 2 * v + acc, 2 * Grid);
                level = f;
                velocity = v;
                acceleration = acc;
            }
            if (!double.IsFinite(values[i]))
                break;
        }
        return values;
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
        CompetitorData.FromCloses([1, 4, 2, 8, -3, 9, 0, 1, 3, -2, 5, 0]);
}

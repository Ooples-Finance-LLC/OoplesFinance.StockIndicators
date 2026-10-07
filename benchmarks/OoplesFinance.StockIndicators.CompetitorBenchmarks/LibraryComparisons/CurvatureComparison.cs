using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class CurvatureComparison
{
    internal static readonly string[] Names =
    [
        "Curvature",
        "Intercept",
        "StdDev",
        "RSquared",
        "Line",
    ];
    internal static readonly ComparisonPair Pair = new(
        "QuanTAlib.Curvature",
        "WindowRegressionStatistics.Of(WindowLinearRegression.Slope)",
        Native,
        (d, p) => Owned(d.IndicatorBars, p),
        (d, p) => Reference(d.Closes, p, false),
        Names,
        CompetitorReference: (d, p) => Reference(d.Closes, p, true),
        ErrorBudget: IndicatorErrorBudget.Exact
    );

    private static ComparisonSeries Series(double?[][] values) =>
        new(
            Names
                .Select(
                    (name, slot) =>
                        new KeyValuePair<string, ComparisonOutput>(
                            name,
                            new(
                                0,
                                values[slot].Select(v => v ?? double.NaN).ToArray(),
                                values[slot].Select(v => v.HasValue).ToArray()
                            )
                        )
                )
                .ToDictionary(kv => kv.Key, kv => kv.Value)
        );

    private static ComparisonSeries Native(CompetitorData data, int period)
    {
        var indicator = new QuanTAlib.Curvature(period);
        var values = Enumerable.Range(0, 5).Select(_ => new double?[data.Count]).ToArray();
        for (var i = 0; i < data.Count; i++)
        {
            values[0][i] = indicator.Calc(new QuanTAlib.TValue(data.Closes[i], true, false)).Value;
            values[1][i] = indicator.Intercept;
            values[2][i] = indicator.StdDev;
            values[3][i] = indicator.RSquared;
            values[4][i] = indicator.Line;
        }
        return Series(values);
    }

    internal static ComparisonSeries Owned(double[] prices, int period) =>
        Owned(
            prices
                .Select((x, i) => new Bar(DateTime.UnixEpoch.AddDays(i), x, x, x, x, 1))
                .ToArray(),
            period
        );

    private static ComparisonSeries Owned(IReadOnlyList<Bar> bars, int period)
    {
        // Select only the first slope, so an unused first-stage statistic cannot
        // reject an otherwise representable second-stage result.
        var slope = new WindowLinearRegression(period, WindowRegressionOutput.Slope);
        var curvature = new WindowRegressionStatistics(period);
        curvature.Of(slope);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(curvature)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return Series(
            Enumerable
                .Range(0, 5)
                .Select(slot =>
                {
                    var values = run[curvature.Outputs[slot]].ToArray();
                    var flags = run[curvature.Outputs[slot + 5]].ToArray();
                    return values.Select((v, i) => flags[i] > 0 ? (double?)v : null).ToArray();
                })
                .ToArray()
        );
    }

    internal static ComparisonSeries Reference(double[] prices, int period, bool native)
    {
        var first = new double[prices.Length];
        var values = Enumerable.Range(0, 5).Select(_ => new double?[prices.Length]).ToArray();
        double? innerRetained = null,
            outerRetained = null;
        for (var i = 0; i < prices.Length; i++)
        {
            var start = Math.Max(0, i - period + 1);
            var source = prices[start..(i + 1)];
            first[i] = (native ? NativeFit(source, ref innerRetained) : ExactFit(source))[0]!.Value;
            var window = first[start..(i + 1)];
            var fit = native ? NativeFit(window, ref outerRetained) : ExactFit(window);
            for (var slot = 0; slot < 5; slot++)
                values[slot][i] = fit[slot];
        }
        return Series(values);
    }

    private static double?[] ExactFit(double[] prices)
    {
        if (prices.Length < 2)
            return [0, null, null, null, null];
        var n = new BigInteger(prices.Length);
        var x = n * (n + 1) / 2;
        var xx = n * (n + 1) * (2 * n + 1) / 6;
        BigInteger y = 0,
            xy = 0,
            yy = 0;
        for (var i = 0; i < prices.Length; i++)
        {
            var value = Units(prices[i]);
            y += value;
            xy += (i + 1) * value;
            yy += value * value;
        }
        var horizontal = n * xx - x * x;
        var covariance = n * xy - x * y;
        var variance = n * yy - y * y;
        return
        [
            Round(covariance, horizontal * Grid),
            Round(y * horizontal - covariance * x, n * horizontal * Grid),
            DispersionReferenceArithmetic.Sqrt(variance, n * n * Grid * Grid),
            variance.IsZero ? null : Round(covariance * covariance, horizontal * variance),
            Round(y * horizontal + covariance * (n * n - x), n * horizontal * Grid),
        ];
    }

    // Model each pinned native binary64 operation, independently from production's moments.
    private static double?[] NativeFit(double[] prices, ref double? retained)
    {
        var count = prices.Length;
        if (count < 2)
            return [0, null, null, null, null];
        var meanX = Divide(
            Enumerable.Range(1, count).Aggregate(0d, (sum, x) => Add(sum, x)),
            count
        );
        var meanY = Divide(prices.Aggregate(0d, Add), count);
        double xx = 0,
            xy = 0,
            yy = 0;
        for (var i = 0; i < count; i++)
        {
            var dx = Subtract(i + 1, meanX);
            var dy = Subtract(prices[i], meanY);
            xx = Add(xx, Multiply(dx, dx));
            yy = Add(yy, Multiply(dy, dy));
            xy = Add(xy, Multiply(dx, dy));
        }
        var slope = Divide(xy, xx);
        var intercept = Subtract(meanY, Multiply(slope, meanX));
        var sx = DispersionReferenceArithmetic.Sqrt(Units(Divide(xx, count)), Grid);
        var sy = DispersionReferenceArithmetic.Sqrt(Units(Divide(yy, count)), Grid);
        var product = Multiply(sx, sy);
        if (product != 0) // NOSONAR: The native formula distinguishes an exactly underflowed product.
        {
            var correlation = Divide(Divide(xy, product), count);
            retained = Multiply(correlation, correlation);
        }
        return [slope, intercept, sy, retained, Add(Multiply(slope, count), intercept)];
    }

    internal static CompetitorData Fixture() =>
        CompetitorData.FromCloses([1, 2, 4, 8, 16, 3, 3, 3, 3, 3, 3, 7, 2, 11, -1]);
}

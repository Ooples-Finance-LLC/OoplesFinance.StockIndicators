using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;
using TALib;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class WindowRegressionComparison
{
    internal static readonly ComparisonPair[] Pairs =
    [
        .. new[] { "LinearReg", "LinearRegSlope", "LinearRegIntercept", "LinearRegAngle", "Tsf" }.Select(
            name => new ComparisonPair(
                "TaLib.Functions." + name,
                "WindowLinearRegression",
                (d, p) => TaLib(name, d, p),
                (d, p) => Ooples(Kind(name), d, p),
                (d, p) => Reference(Kind(name), d, p)
            )
        ),
        new(
            "Skender.GetEpma",
            "WindowLinearRegression",
            (d, p) =>
                new(
                    Math.Min(p - 1, d.Count),
                    d.Quotes.GetEpma(p).Select(r => r.Epma ?? double.NaN).ToArray()
                ),
            (d, p) => Ooples(WindowRegressionOutput.Endpoint, d, p),
            (d, p) => Reference(WindowRegressionOutput.Endpoint, d, p)
        ),
    ];

    private static WindowRegressionOutput Kind(string name) =>
        name switch
        {
            "LinearRegSlope" => WindowRegressionOutput.Slope,
            "LinearRegIntercept" => WindowRegressionOutput.Intercept,
            "LinearRegAngle" => WindowRegressionOutput.Angle,
            "Tsf" => WindowRegressionOutput.Forecast,
            _ => WindowRegressionOutput.Endpoint,
        };

    private static ComparisonSeries Ooples(
        WindowRegressionOutput kind,
        CompetitorData data,
        int period
    )
    {
        var indicator = new WindowLinearRegression(period, kind);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return new(Math.Min(period - 1, data.Count), run[indicator.Outputs[0]].ToArray());
    }

    internal static TALib.Core.RetCode Native(
        string name,
        CompetitorData data,
        double[] values,
        int period,
        out System.Range range
    ) =>
        name switch
        {
            "LinearRegAngle" => Functions.LinearRegAngle<double>(
                data.Closes,
                System.Range.All,
                values,
                out range,
                period
            ),
            "LinearRegSlope" => Functions.LinearRegSlope<double>(
                data.Closes,
                System.Range.All,
                values,
                out range,
                period
            ),
            "LinearRegIntercept" => Functions.LinearRegIntercept<double>(
                data.Closes,
                System.Range.All,
                values,
                out range,
                period
            ),
            "Tsf" => Functions.Tsf<double>(
                data.Closes,
                System.Range.All,
                values,
                out range,
                period
            ),
            _ => Functions.LinearReg<double>(
                data.Closes,
                System.Range.All,
                values,
                out range,
                period
            ),
        };

    private static ComparisonSeries TaLib(string name, CompetitorData data, int period)
    {
        var packed = new double[data.Count];
        var code = Native(name, data, packed, period, out var range);
        var first = Math.Min(period - 1, data.Count);
        if (data.Count == 1 && code == TALib.Core.RetCode.OutOfRangeParam)
            return new(first, [double.NaN]);
        if (code != TALib.Core.RetCode.Success)
            throw new InvalidOperationException("TA-Lib regression returned " + code);
        var (start, count) = range.GetOffsetAndLength(data.Count);
        if (count != data.Count - first || count > 0 && start != first)
            throw new InvalidOperationException("Unexpected regression alignment.");
        var values = Enumerable.Repeat(double.NaN, data.Count).ToArray();
        Array.Copy(packed, 0, values, start, count);
        return new(first, values);
    }

    private static ComparisonSeries Reference(
        WindowRegressionOutput kind,
        CompetitorData data,
        int period
    )
    {
        var result = new double[data.Count];
        var n = new BigInteger(period);
        var spread = n * n - 1;
        for (var i = period - 1; i < data.Count; i++)
        {
            var sum = BigInteger.Zero;
            var weighted = BigInteger.Zero;
            for (var j = 0; j < period; j++)
            {
                var value = Units(data.Closes[i - period + 1 + j]);
                sum += value;
                weighted += j * value;
            }
            var covariance = 2 * weighted - (n - 1) * sum;
            if (kind == WindowRegressionOutput.Angle)
            {
                result[i] = Math.Atan(Round(6 * covariance, Grid * n * spread)) * (180 / Math.PI);
                continue;
            }
            var position =
                kind == WindowRegressionOutput.Intercept ? 1 - n
                : kind == WindowRegressionOutput.Forecast ? n + 1
                : n - 1;
            result[i] = Round(
                kind == WindowRegressionOutput.Slope
                    ? 6 * covariance
                    : sum * spread + 3 * covariance * position,
                Grid * n * spread
            );
        }
        return new(Math.Min(period - 1, data.Count), result);
    }

    internal static CompetitorData Fixture() =>
        CompetitorData.FromCloses([1, 2, 4, 9, -2, 8, 0, -3, 2, 2, 2, 17]);
}

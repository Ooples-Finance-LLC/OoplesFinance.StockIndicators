using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class ConvolutionComparison
{
    internal static readonly ComparisonPair[] Pairs = [Create(true), Create(false)];

    internal static ComparisonPair Create(bool endpoint, double[]? kernel = null) =>
        new(
            endpoint ? "QuanTAlib.Epma" : "QuanTAlib.Convolution",
            endpoint ? "EndpointWeightedAverage" : "NormalizedConvolution",
            (d, p) => Competitor(d, p, endpoint, kernel),
            (d, p) => Ooples(d, p, endpoint, kernel),
            (d, p) => Reference(d, p, endpoint, kernel, false),
            CompetitorReference: (d, p) => Reference(d, p, endpoint, kernel, true)
        );

    private static double[] Weights(int period, bool endpoint, double[]? kernel) =>
        kernel
        ?? Enumerable
            .Range(0, period)
            .Select(lag => endpoint ? (double)(2L * period - 1 - 3L * lag) : lag + 1d)
            .ToArray();

    private static ComparisonSeries Competitor(
        CompetitorData data,
        int period,
        bool endpoint,
        double[]? kernel
    )
    {
        QuanTAlib.AbstractBase indicator = endpoint
            ? new QuanTAlib.Epma(period)
            : new QuanTAlib.Convolution(Weights(period, false, kernel));
        return new(
            0,
            data.Closes.Select(v => indicator.Calc(new QuanTAlib.TValue(v, true, false)).Value)
                .ToArray()
        );
    }

    private static ComparisonSeries Ooples(
        CompetitorData data,
        int period,
        bool endpoint,
        double[]? kernel
    )
    {
        IIndicator indicator = endpoint
            ? new EndpointWeightedAverage(period)
            : new NormalizedConvolution(Weights(period, false, kernel));
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return new(0, run[indicator.Outputs[0]].ToArray());
    }

    private static double[] NativeEndpointWeights(int period)
    {
        var weights = Weights(period, true, null);
        var sum = weights.Aggregate(0d, Add);
        return weights.Select(w => Divide(w, sum)).ToArray();
    }

    private static ComparisonSeries Reference(
        CompetitorData data,
        int period,
        bool endpoint,
        double[]? kernel,
        bool native
    )
    {
        var weights =
            native && endpoint ? NativeEndpointWeights(period) : Weights(period, endpoint, kernel);
        var result = new double[data.Count];
        for (var i = 0; i < data.Count; i++)
        {
            var count = Math.Min(i + 1, weights.Length);
            if (native)
            {
                var mass = weights.Take(count).Aggregate(0d, Add);
                var divisor = mass == 0 ? count : mass;
                var sum = 0d;
                for (var lag = 0; lag < count; lag++)
                    sum = Add(sum, Multiply(data.Closes[i - lag], Divide(weights[lag], divisor)));
                if (endpoint && count < period)
                    sum = Divide(sum, NativeEndpointWeights(count).Aggregate(0d, Add));
                result[i] = sum;
            }
            else
            {
                var top = BigInteger.Zero;
                var mass = BigInteger.Zero;
                for (var lag = 0; lag < count; lag++)
                {
                    var w = Units(weights[lag]);
                    top += w * Units(data.Closes[i - lag]);
                    mass += w;
                }
                result[i] = Round(top, (mass.IsZero ? Grid * count : mass) * Grid);
            }
        }
        return new(0, result);
    }
}

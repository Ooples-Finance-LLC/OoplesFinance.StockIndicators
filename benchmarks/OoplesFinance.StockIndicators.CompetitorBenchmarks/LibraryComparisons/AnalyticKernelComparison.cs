using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class AnalyticKernelComparison
{
    internal static readonly ComparisonPair[] Pairs = [Create(true), Create(false)];

    internal static ComparisonPair Create(bool gaussian, double? sigma = null) =>
        new(
            gaussian ? "QuanTAlib.Gma" : "QuanTAlib.Sinema",
            gaussian ? nameof(GaussianWeightedAverage) : nameof(SineWeightedAverage),
            (d, p) => Native(d, p, gaussian, sigma),
            (d, p) => Owned(d.IndicatorBars, p, gaussian, sigma ?? 1),
            (d, p) => Reference(d, p, gaussian, sigma ?? 1, false),
            CompetitorReference: (d, p) => Reference(d, p, gaussian, sigma ?? 1, true),
            ErrorBudget: IndicatorErrorBudget.Exact
        );

    internal static QuanTAlib.AbstractBase NativeIndicator(
        int period,
        bool gaussian,
        double? sigma = null
    ) =>
        gaussian
            ? sigma.HasValue
                ? new QuanTAlib.Convolution(QuanTAlib.Gma.GenerateKernel(period, sigma.Value))
                : new QuanTAlib.Gma(period)
            : new QuanTAlib.Sinema(period);

    private static ComparisonSeries Native(
        CompetitorData data,
        int period,
        bool gaussian,
        double? sigma
    )
    {
        var indicator = NativeIndicator(period, gaussian, sigma);
        return new(
            0,
            data.Closes.Select(x => indicator.Calc(new QuanTAlib.TValue(x, true, false)).Value)
                .ToArray()
        );
    }

    internal static ComparisonSeries Owned(
        IReadOnlyList<Bar> bars,
        int period,
        bool gaussian,
        double sigma = 1
    )
    {
        IIndicator indicator = gaussian
            ? new GaussianWeightedAverage(period, sigma)
            : new SineWeightedAverage(period);
        using var run = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        return new(0, run[indicator.Outputs[0]].ToArray());
    }

    // Construct the formula independently and round each specified binary64 operation.
    // Elementary functions define the rounded coefficient contract; separate tests compare
    // Gaussian coefficients against a high-precision exponential series.
    internal static double[] Weights(
        int period,
        bool gaussian,
        double sigma,
        bool native,
        int? available = null
    )
    {
        var weights = new double[available ?? period];
        for (var lag = 0; lag < weights.Length; lag++)
        {
            if (gaussian)
            {
                var center = period / 2;
                var x = Divide(lag - center, center);
                var argument = native
                    ? -Divide(Multiply(x, x), Multiply(Multiply(2, sigma), sigma))
                    : Multiply(Multiply(-.5, Divide(x, sigma)), Divide(x, sigma));
                weights[lag] = Math.Exp(argument);
            }
            else
                weights[lag] = Math.Sin(Divide(Multiply(lag + 1d, Math.PI), period + 1d));
        }
        if (native)
        {
            var mass = weights.Aggregate(0d, Add);
            for (var lag = 0; lag < weights.Length; lag++)
                weights[lag] = Divide(weights[lag], mass);
        }
        return weights;
    }

    internal static ComparisonSeries Reference(
        CompetitorData data,
        int period,
        bool gaussian,
        double sigma,
        bool native
    )
    {
        var weights = Weights(
            period,
            gaussian,
            sigma,
            native,
            native ? null : Math.Min(period, data.Count)
        );
        // Reuse the independently modeled convolution arithmetic, not either runtime implementation.
        var pair = ConvolutionComparison.Create(false, weights.Length == 0 ? [1] : weights);
        return native ? pair.CompetitorReference!(data, period) : pair.Reference!(data, period);
    }

    internal static CompetitorData Fixture() =>
        CompetitorData.FromCloses([0, 1, -1, 2, 4, 8, 3, 3, -7, 13, 21, 0, 0, 0, 0, 0, 2, -3]);
}

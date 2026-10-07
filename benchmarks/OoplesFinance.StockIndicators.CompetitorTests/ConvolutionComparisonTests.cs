using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class ConvolutionComparisonTests
{
    public static IEnumerable<object[]> Kernels =>
        new double[][]
        {
            [1],
            [1, 2, 3],
            [3, 2, 1],
            [1, -1],
            [0, 0, 1],
            [-2, -3, -1],
            [.1, .2, -.3],
            [0, 0, 0],
        }.Select(k => new object[] { k });

    [Theory, MemberData(nameof(Kernels))]
    public async Task SignedAndZeroMassKernelsHaveIndependentLifecycleReferences(double[] kernel)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(NormalizedConvolution),
                "kernel",
                () => new NormalizedConvolution(kernel)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        ComparisonVerifier.Check(
            ConvolutionComparison.Create(false, kernel),
            WindowRegressionComparison.Fixture(),
            kernel.Length
        );
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(20)]
    public async Task EndpointWeightsAndLifecycle(int period)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(EndpointWeightedAverage),
                "fixed endpoint weights",
                () => new EndpointWeightedAverage(period)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        ComparisonVerifier.Check(
            ConvolutionComparison.Create(true),
            WindowRegressionComparison.Fixture(),
            period
        );
    }

    [Fact]
    public async Task NewestFirstOrientationAndZeroMassFallbackAreExplicit()
    {
        var data = CompetitorData.FromCloses([3, 9, 15]);
        var weighted = new NormalizedConvolution([1, 2]);
        var difference = new NormalizedConvolution([1, -1]);
        var zeroPrefix = new NormalizedConvolution([0, 0, 1]);
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(weighted, difference, zeroPrefix)
            .BuildAsync();
        Assert.Equal(new[] { 3d, 5, 11 }, run[weighted.Outputs[0]].ToArray());
        Assert.Equal(new[] { 3d, 3, 3 }, run[difference.Outputs[0]].ToArray());
        Assert.Equal(new[] { 0d, 0, 3 }, run[zeroPrefix.Outputs[0]].ToArray());
    }

    [Fact]
    public async Task EndpointStartupUsesFullPeriodWeightsThenMatchesRegression()
    {
        var endpoint = new EndpointWeightedAverage(3);
        var fit = new WindowLinearRegression(3);
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(CompetitorData.FromCloses([1, 2, 4, 9]).IndicatorBars))
            .ConfigureIndicators(endpoint, fit)
            .BuildAsync();
        Assert.Equal(new[] { 1d, 12d / 7, 23d / 6, 8.5 }, run[endpoint.Outputs[0]].ToArray());
        Assert.Equal(2, run[fit.Outputs[0]].ToArray()[1]);
        Assert.Equal(
            run[fit.Outputs[0]].ToArray().Skip(2),
            run[endpoint.Outputs[0]].ToArray().Skip(2)
        );
    }

    [Fact]
    public async Task ConfigurationIsCopiedAndInvalidKernelsAreRejected()
    {
        var weights = new[] { 1d, 2 };
        var indicator = new NormalizedConvolution(weights);
        weights[0] = 100;
        Assert.Equal(new[] { 1d, 2 }, indicator.Kernel);
        Assert.Throws<NotSupportedException>(() => ((IList<double>)indicator.Kernel)[0] = 100);
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(CompetitorData.FromCloses([3, 9]).IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        Assert.Equal(new[] { 3d, 5 }, run[indicator.Outputs[0]].ToArray());
        Assert.Throws<ArgumentNullException>(() => new NormalizedConvolution(null!));
        Assert.Throws<ArgumentException>(() => new NormalizedConvolution([]));
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            Assert.Throws<ArgumentException>(() => new NormalizedConvolution([1, bad]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EndpointWeightedAverage(0));
    }

    [Fact]
    public async Task HugeWeightsAndSubnormalMassPreserveFiniteRatios()
    {
        foreach (
            var weights in new[]
            {
                new[] { double.MaxValue, double.MaxValue },
                new[] { double.Epsilon, double.Epsilon },
            }
        )
        {
            var indicator = new NormalizedConvolution(weights);
            var bars = Enumerable
                .Range(0, 3)
                .Select(i => new Bar(
                    DateTime.UnixEpoch.AddDays(i),
                    double.MaxValue,
                    double.MaxValue,
                    double.MaxValue,
                    double.MaxValue,
                    0
                ))
                .ToArray();
            using var run = await new StockIndicatorBuilder()
                .ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(indicator)
                .BuildAsync();
            Assert.All(
                run[indicator.Outputs[0]].ToArray(),
                value => Assert.Equal(double.MaxValue, value)
            );
        }
        var large = new EndpointWeightedAverage(int.MaxValue);
        using var largeRun = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(CompetitorData.FromCloses([7, 7, 7]).IndicatorBars))
            .ConfigureIndicators(large)
            .BuildAsync();
        Assert.Equal(new[] { 7d, 7, 7 }, largeRun[large.Outputs[0]].ToArray());
    }

    [Fact]
    public void IndependentNativeRoundingDoesNotMaskCorruption()
    {
        foreach (
            var pair in new[]
            {
                ConvolutionComparison.Create(true),
                ConvolutionComparison.Create(false, [.1, .2, -.3]),
            }
        )
        foreach (var native in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData data, int period)
            {
                var result = native ? pair.Competitor(data, period) : pair.Ooples(data, period);
                result.Outputs["Value"].Values[0] += 1;
                return result;
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    WindowRegressionComparison.Fixture(),
                    3
                )
            );
        }
    }
}

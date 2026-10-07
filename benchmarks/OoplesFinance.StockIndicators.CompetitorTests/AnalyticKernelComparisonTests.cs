using System.Numerics;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class AnalyticKernelComparisonTests
{
    [Theory]
    [InlineData(true, 2)]
    [InlineData(true, 3)]
    [InlineData(true, 20)]
    [InlineData(false, 1)]
    [InlineData(false, 3)]
    [InlineData(false, 20)]
    public async Task FullOutputsAndOwnedLifecycle(bool gaussian, int period)
    {
        ComparisonVerifier.Check(
            AnalyticKernelComparison.Create(gaussian),
            AnalyticKernelComparison.Fixture(),
            period
        );
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                gaussian ? typeof(GaussianWeightedAverage) : typeof(SineWeightedAverage),
                "analytic kernel",
                () =>
                    gaussian ? new GaussianWeightedAverage(period) : new SineWeightedAverage(period)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Theory]
    [InlineData(.25)]
    [InlineData(2)]
    [InlineData(-2)]
    public async Task GaussianWidthCoversKernelFactoryAndLifecycle(double sigma)
    {
        foreach (var period in new[] { 2, 3, 20 })
            ComparisonVerifier.Check(
                AnalyticKernelComparison.Create(true, sigma),
                AnalyticKernelComparison.Fixture(),
                period
            );
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(GaussianWeightedAverage),
                "Gaussian width",
                () => new GaussianWeightedAverage(3, sigma)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void KernelCoefficientsHaveIndependentSeriesChecks()
    {
        var budget = new IndicatorErrorBudget(0, 4e-15, true);
        foreach (var period in new[] { 2, 3, 20, 101 })
        {
            foreach (var gaussian in new[] { true, false })
            {
                var raw = AnalyticKernelComparison.Weights(period, gaussian, 1, false);
                for (var i = 0; i < period; i++)
                {
                    var center = period / 2;
                    var x = Divide(i - center, center);
                    var expected = gaussian
                        ? TranscendentalComparison
                            .ReferenceValue("Exp", Multiply(Multiply(-.5, x), x))!
                            .Value
                        : SineSeries(Divide(Multiply(i + 1d, Math.PI), period + 1d));
                    Assert.True(budget.Accepts(expected, raw[i]));
                }
                Assert.Equal(
                    AnalyticKernelComparison.Weights(period, gaussian, 1, true),
                    gaussian
                        ? QuanTAlib.Gma.GenerateKernel(period)
                        : QuanTAlib.Sinema.GenerateKernel(period)
                );
            }
        }
    }

    private static double SineSeries(double argument)
    {
        var scale = BigInteger.One << 192;
        var x = Units(argument) * scale / Grid;
        var square = x * x / scale;
        var term = x;
        var sum = x;
        for (var n = 1; n <= 80; n++)
        {
            term = -term * square / (scale * (2 * n) * (2 * n + 1));
            sum += term;
        }
        return Round(sum, scale);
    }

    [Fact]
    public void WideAndSubnormalPricesAndLazyMaximumPeriods()
    {
        foreach (var gaussian in new[] { true, false })
        foreach (var period in new[] { 3, int.MaxValue })
        foreach (var price in new[] { double.MaxValue, -double.MaxValue, double.Epsilon })
        {
            var bars = Enumerable
                .Range(0, 4)
                .Select(i => new Bar(DateTime.UnixEpoch.AddDays(i), price, price, price, price, 1))
                .ToArray();
            Assert.All(
                AnalyticKernelComparison.Owned(bars, period, gaussian).Outputs["Value"].Values,
                actual => Assert.Equal(price, actual)
            );
        }
        var tiny = CompetitorData.FromCloses([
            0,
            double.Epsilon,
            2 * double.Epsilon,
            4 * double.Epsilon,
        ]);
        foreach (var gaussian in new[] { true, false })
            ComparisonVerifier.Check(AnalyticKernelComparison.Create(gaussian), tiny, 3);
    }

    [Fact]
    public void NativeUndefinedWidthsAndPeriodOneAreExplicit()
    {
        Assert.True(
            double.IsNaN(new QuanTAlib.Gma(1).Calc(new QuanTAlib.TValue(3, true, false)).Value)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => new GaussianWeightedAverage(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SineWeightedAverage(0));
        foreach (var sigma in new[] { 0d, double.NaN, double.PositiveInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() => new GaussianWeightedAverage(3, sigma));
        foreach (var sigma in new[] { 0d, double.Epsilon, double.NaN })
            Assert.All(
                QuanTAlib.Gma.GenerateKernel(3, sigma),
                weight => Assert.True(double.IsNaN(weight))
            );
        var data = CompetitorData.FromCloses([3, 9, 15, 21]);
        Assert.Equal(
            new[] { 0d, 3, 9, 15 },
            AnalyticKernelComparison
                .Owned(data.IndicatorBars, 3, true, double.Epsilon)
                .Outputs["Value"]
                .Values
        );
        Assert.Equal(
            new[] { 3d, 6, 9, 15 },
            AnalyticKernelComparison
                .Owned(data.IndicatorBars, 3, true, double.MaxValue)
                .Outputs["Value"]
                .Values
        );
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NativeSourceRevisionAndResetHistory(bool gaussian)
    {
        var source = new QuanTAlib.TSeries();
        QuanTAlib.AbstractBase subscribed = gaussian
            ? new QuanTAlib.Gma(source, 3)
            : new QuanTAlib.Sinema(source, 3);
        var direct = AnalyticKernelComparison.NativeIndicator(3, gaussian);
        foreach (var x in new[] { 1d, 2, 4 })
        {
            var input = new QuanTAlib.TValue(x, true, false);
            source.Add(input);
            Assert.Equal(direct.Calc(input).Value, subscribed.Value);
        }
        Assert.True(direct.IsHot);
        direct.Calc(new QuanTAlib.TValue(7, false, false));
        var fresh = AnalyticKernelComparison.NativeIndicator(3, gaussian);
        foreach (var x in new[] { 1d, 2, 7 })
            fresh.Calc(new QuanTAlib.TValue(x, true, false));
        Assert.Equal(fresh.Value, direct.Value);
        direct.Init();
        var continued = fresh.Calc(new QuanTAlib.TValue(10, true, false)).Value;
        Assert.Equal(continued, direct.Calc(new QuanTAlib.TValue(10, true, false)).Value);
        Assert.False(direct.IsHot);
        Assert.NotEqual(10d, direct.Value);
    }

    [Fact]
    public void CorruptedKernelOrientationAndValuesFail()
    {
        foreach (var pair in AnalyticKernelComparison.Pairs)
        foreach (var native in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData data, int period)
            {
                var result = native ? pair.Competitor(data, period) : pair.Ooples(data, period);
                result.Outputs["Value"].Values[1] += 1;
                return result;
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    AnalyticKernelComparison.Fixture(),
                    3
                )
            );
        }
        var gaussian = AnalyticKernelComparison.Create(true);
        var reversed = gaussian with
        {
            Library = (data, period) =>
                ConvolutionComparison
                    .Create(
                        false,
                        AnalyticKernelComparison.Weights(period, true, 1, false).Reverse().ToArray()
                    )
                    .Ooples(data, period),
        };
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Check(reversed, AnalyticKernelComparison.Fixture(), 4)
        );
    }
}

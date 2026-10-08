using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class WindowRegressionComparisonTests
{
    [Theory]
    [InlineData(WindowRegressionOutput.Endpoint)]
    [InlineData(WindowRegressionOutput.Forecast)]
    [InlineData(WindowRegressionOutput.Slope)]
    [InlineData(WindowRegressionOutput.Intercept)]
    [InlineData(WindowRegressionOutput.Angle)]
    public async Task IndependentCenteredReferenceLifecycleAndOverflow(
        WindowRegressionOutput output
    )
    {
        foreach (var period in new[] { 1, 2, 3, 20 })
        {
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    typeof(WindowLinearRegression),
                    "window regression",
                    () => new WindowLinearRegression(period, output)
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
    }

    [Fact]
    public async Task LocalInterceptAndForecastUseTheCorrectWindowCoordinates()
    {
        var data = CompetitorData.FromCloses([1, 2, 4, 9]);
        var indicators = Enum.GetValues<WindowRegressionOutput>()
            .Select(k => new WindowLinearRegression(3, k))
            .ToArray();
        var old = new LinReg(3);
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicators.Cast<IIndicator>().Append(old).ToArray())
            .BuildAsync();
        Assert.Equal(new[] { 1d, 2, 23d / 6, 8.5 }, run[indicators[0].Outputs[0]].ToArray());
        Assert.Equal(new[] { 1d, 3, 16d / 3, 12 }, run[indicators[1].Outputs[0]].ToArray());
        Assert.Equal(new[] { 0d, 1, 1.5, 3.5 }, run[indicators[2].Outputs[0]].ToArray());
        Assert.Equal(new[] { 1d, 1, 5d / 6, 1.5 }, run[indicators[3].Outputs[0]].ToArray());
        Assert.Equal(-2, run[old.Outputs[3]].ToArray()[3]);
        foreach (var slot in new[] { 0, 1, 2 })
            Assert.Equal(
                run[old.Outputs[slot]].ToArray(),
                run[indicators[slot].Outputs[0]].ToArray()
            );
    }

    [Fact]
    public async Task UnrepresentableUnselectedSlopeDoesNotInvalidateEndpointOrIntercept()
    {
        var bars = new[] { -double.MaxValue, double.MaxValue }
            .Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 0))
            .ToArray();
        var endpoint = new WindowLinearRegression(2);
        var intercept = new WindowLinearRegression(2, WindowRegressionOutput.Intercept);
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(endpoint, intercept)
            .BuildAsync();
        Assert.Equal(
            new[] { -double.MaxValue, double.MaxValue },
            run[endpoint.Outputs[0]].ToArray()
        );
        Assert.Equal(
            new[] { -double.MaxValue, -double.MaxValue },
            run[intercept.Outputs[0]].ToArray()
        );
    }

    [Fact]
    public async Task SubnormalSlopesAndMaximumPeriodsRemainExactAndLazy()
    {
        var bars = new[] { 0d, double.Epsilon, 2 * double.Epsilon }
            .Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 0))
            .ToArray();
        var slope = new WindowLinearRegression(int.MaxValue, WindowRegressionOutput.Slope);
        var forecast = new WindowLinearRegression(int.MaxValue, WindowRegressionOutput.Forecast);
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(slope, forecast)
            .BuildAsync();
        Assert.Equal(new[] { 0d, double.Epsilon, double.Epsilon }, run[slope.Outputs[0]].ToArray());
        Assert.Equal(
            new[] { 0d, 2 * double.Epsilon, 3 * double.Epsilon },
            run[forecast.Outputs[0]].ToArray()
        );
    }

    [Fact]
    public void WindowBoundariesAndNativePeriodOneRejectionsArePinned()
    {
        var data = WindowRegressionComparison.Fixture();
        foreach (var pair in WindowRegressionComparison.Pairs)
        foreach (var period in new[] { 2, 3, 20 })
        {
            ComparisonVerifier.Check(pair, data, period);
            ComparisonVerifier.Check(pair, CompetitorData.FromCloses([5]), period);
        }
        foreach (var name in new[] { "LinearReg", "LinearRegSlope", "LinearRegIntercept", "LinearRegAngle", "Tsf" })
            Assert.Equal(
                TALib.Core.RetCode.BadParam,
                WindowRegressionComparison.Native(name, data, new double[data.Count], 1, out _)
            );
        Assert.Throws<ArgumentOutOfRangeException>(() => data.Quotes.GetEpma(1).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowLinearRegression(0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WindowLinearRegression(2, (WindowRegressionOutput)100)
        );
    }

    [Fact]
    public void EverySelectedOutputRejectsCorruptionInBothComparisonArms()
    {
        foreach (var pair in WindowRegressionComparison.Pairs)
        foreach (var native in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData data, int period)
            {
                var result = native ? pair.Competitor(data, period) : pair.Ooples(data, period);
                result.Outputs["Value"].Values[period] += 1;
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

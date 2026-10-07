using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class MeanStartupEndpointComparisonTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(20)]
    public async Task MeanStartupIndependentLifecycleContract(int period)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(EndpointWeightedAverage),
                "mean startup",
                () => new EndpointWeightedAverage(period, true)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public async Task MeanStartupTransitionsToEndpointInsteadOfWilderRecurrence()
    {
        var data = CompetitorData.FromCloses([1, 2, 4, 9]);
        var indicator = new EndpointWeightedAverage(3, true);
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        Assert.Equal(new[] { 1d, 1.5, 23d / 6, 8.5 }, run[indicator.Outputs[0]].ToArray());
        Assert.True(indicator.AverageDuringWarmup);
        foreach (var period in new[] { 2, 3, 20 })
            ComparisonVerifier.Check(
                MeanStartupEndpointComparison.Pair,
                WindowRegressionComparison.Fixture(),
                period
            );
        Assert.Throws<ArgumentOutOfRangeException>(() => new QuanTAlib.Mma(1));
    }

    [Fact]
    public async Task NativeInt32DenominatorOverflowDoesNotAffectOurEndpoint()
    {
        const int period = 46341;
        var native = new QuanTAlib.Mma(period);
        for (var i = 0; i < period - 1; i++)
            native.Calc(new QuanTAlib.TValue(10, true, false));
        var wrong = native.Calc(new QuanTAlib.TValue(20, true, false)).Value;
        Assert.True(
            wrong < 10,
            "Pinned native overflowing denominator should reverse the final trend correction."
        );
        var bars = Enumerable
            .Range(0, period)
            .Select(i =>
            {
                var value = i == period - 1 ? 20d : 10d;
                return new Bar(DateTime.UnixEpoch.AddMinutes(i), value, value, value, value, 0);
            })
            .ToArray();
        var indicator = new EndpointWeightedAverage(period, true);
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        var values = run[indicator.Outputs[0]].ToArray();
        Assert.All(values.Take(period - 1), value => Assert.Equal(10, value));
        // Endpoint leverage of the newest observation is (4*n-2)/(n*(n+1)).
        var expected = 10m + 10m * (4m * period - 2) / ((decimal)period * (period + 1));
        Assert.Equal((double)expected, values[^1]);
        Assert.True(values[^1] > 10);
    }

    [Fact]
    public async Task MaximumPeriodMeanStartupUsesOnlyReceivedHistory()
    {
        var indicator = new EndpointWeightedAverage(int.MaxValue, true);
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(CompetitorData.FromCloses([1, 2, 4]).IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        Assert.Equal(new[] { 1d, 1.5, 7d / 3 }, run[indicator.Outputs[0]].ToArray());
    }

    [Fact]
    public void IndependentOraclesDetectWrongValuesOnEitherSide()
    {
        var pair = MeanStartupEndpointComparison.Pair;
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

using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class ShadowDojiCandleTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RequiresBothDojiAndStrictMidpointThresholds(bool upper)
    {
        var data = CandleComparison.ShadowFixture();
        IIndicator indicator = upper ? new DragonflyDojiCandle() : new GravestoneDojiCandle();
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(data.IndicatorBars)).ConfigureIndicators(indicator).BuildAsync();
        var expected = upper ? new[] { 1d, 0, 0, 0, 1, 0, 0, 0, 0, 0 } : new[] { 0d, 1, 0, 0, 0, 1, 0, 0, 0, 1 };
        Assert.Equal(expected, run[indicator.Outputs[0]].ToArray());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NeitherRangeNorMidpointOverflows(bool upper)
    {
        var price = upper ? double.MaxValue : -double.MaxValue;
        var bar = new Bar(DateTime.UnixEpoch, price, double.MaxValue, -double.MaxValue, price, 1);
        IIndicator indicator = upper ? new DragonflyDojiCandle() : new GravestoneDojiCandle();
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(new[] { bar })).ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(1, Assert.Single(run[indicator.Outputs[0]].ToArray()));
    }

    [Fact]
    public async Task SubnormalDistancesRetainTheExactStrictBoundary()
    {
        var bars = new[]
        {
            new Bar(DateTime.UnixEpoch, 9 * double.Epsilon, 10 * double.Epsilon, 0, 9 * double.Epsilon, 1),
            new Bar(DateTime.UnixEpoch.AddDays(1), 10 * double.Epsilon, 11 * double.Epsilon, 0, 10 * double.Epsilon, 1)
        };
        var indicator = new DragonflyDojiCandle();
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(new[] { 0d, 1d }, run[indicator].ToArray());
    }

    [Fact]
    public async Task MidpointBetweenAdjacentSubnormalsIsNotRoundedBeforeComparison()
    {
        var bar = new Bar(DateTime.UnixEpoch, 0, 5 * double.Epsilon, 0, double.Epsilon, 1);
        var indicator = new GravestoneDojiCandle(bodyFraction: 0.5m);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(new[] { bar })).ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(0, Assert.Single(run[indicator].ToArray()));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RejectsInvalidFractionsBeforeCreatingState(bool upper)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(upper, -0.1m, 0.1m));
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(upper, 1.1m, 0.1m));
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(upper, 0.1m, -0.1m));
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(upper, 0.1m, 1.1m));
    }

    private static IIndicator Create(bool upper, decimal body, decimal shadow) => upper
        ? new DragonflyDojiCandle(body, shadow) : new GravestoneDojiCandle(body, shadow);
}

using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class DelayedPhaseWarmupTests
{
    [Theory]
    [InlineData(.5, .05, 0)]
    [InlineData(.01, .99, 0)]
    [InlineData(.99, .01, 0)]
    [InlineData(.01, .01, 0)]
    [InlineData(.99, .99, 0)]
    [InlineData(.5, .05, 1000)]
    public async Task WarmupCoversSeedConvergenceWithoutChangingPublication(double fast, double slow, int suppression)
    {
        var indicator = new DelayedPhaseAdaptiveAverage(fast, slow, suppression);
        var bars = Enumerable.Range(0, indicator.WarmupBars + 32)
            .Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 50, 50, 50, 50, 1));
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator).BuildAsync();
        Assert.All(run[indicator.Mama].ToArray().Skip(indicator.WarmupBars),
            value => Assert.InRange(Math.Abs(value - 50), 0, 1e-6));
        var present = run[indicator.MamaIsDefined].ToArray();
        Assert.All(present.Take(32 + suppression), value => Assert.Equal(0, value));
        Assert.All(present.Skip(32 + suppression), value => Assert.Equal(1, value));
    }

    [Fact]
    public void MaximumSuppressionSaturatesWarmupMetadata() =>
        Assert.Equal(int.MaxValue, new DelayedPhaseAdaptiveAverage(suppression: int.MaxValue).WarmupBars);
}

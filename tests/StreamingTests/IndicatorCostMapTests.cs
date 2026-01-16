using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.StreamingTests;

public sealed class IndicatorCostMapTests
{
    [Fact]
    public void MapsSimpleMovingAverageToLow()
    {
        IndicatorCostMap.GetCost(IndicatorName.SimpleMovingAverage).Should().Be(IndicatorCost.Low);
    }

    [Fact]
    public void MapsEhlersIndicatorsToHigh()
    {
        IndicatorCostMap.GetCost(IndicatorName.EhlersFisherTransform).Should().Be(IndicatorCost.High);
    }

    [Fact]
    public void FilterUsesCostMapWhenProviderMissing()
    {
        var filter = new IndicatorFilter { MaxCost = IndicatorCost.Low };
        var definition = new IndicatorDefinition(IndicatorName.SimpleMovingAverage, IndicatorType.Trend, data => data);
        filter.Matches(definition).Should().BeTrue();
    }
}

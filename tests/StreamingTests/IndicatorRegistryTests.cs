using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.StreamingTests;

public sealed class IndicatorRegistryTests
{
    [Fact]
    public void RegistryIncludesSimpleMovingAverage()
    {
        var definitions = IndicatorRegistry.GetDefinitions();
        definitions.Should().Contain(def => def.Name == IndicatorName.SimpleMovingAverage);
    }

    [Fact]
    public void RegisterAllIndicatorsRespectsFilterAndTimeframes()
    {
        var engine = new StreamingIndicatorEngine(new StreamingIndicatorEngineOptions
        {
            EmitUpdates = false
        });

        var filter = new IndicatorFilter
        {
            IncludeNames = new[] { IndicatorName.SimpleMovingAverage, IndicatorName.ExponentialMovingAverage }
        };

        var timeframes = new[] { BarTimeframe.Tick, BarTimeframe.Seconds(1) };
        var registrations = engine.RegisterAllIndicators("AAPL", timeframes, _ => { },
            new IndicatorSubscriptionOptions { IncludeUpdates = false }, filter);

        registrations.Count.Should().Be(4);
    }
}

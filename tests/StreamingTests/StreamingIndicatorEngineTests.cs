using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.StreamingTests;

public sealed class StreamingIndicatorEngineTests
{
    [Fact]
    public void RegistersIndicatorAndEmitsUpdates()
    {
        var engine = new StreamingIndicatorEngine(new StreamingIndicatorEngineOptions
        {
            EmitUpdates = false
        });

        IndicatorUpdate? last = null;
        using var registration = engine.RegisterIndicator(
            "AAPL",
            BarTimeframe.Tick,
            data => data.CalculateSimpleMovingAverage(2),
            update => last = update,
            new IndicatorSubscriptionOptions { IncludeUpdates = false });

        var start = new DateTime(2024, 1, 2, 9, 30, 0, DateTimeKind.Utc);
        engine.OnTrade(new StreamTrade("AAPL", start, 10, 1));
        engine.OnTrade(new StreamTrade("AAPL", start.AddSeconds(1), 20, 1));

        last.Should().NotBeNull();
        last!.IndicatorData.CustomValuesList.Should().NotBeNullOrEmpty();
        last.IndicatorData.CustomValuesList[^1].Should().BeApproximately(15, 1e-10);
        last.IsFinalBar.Should().BeTrue();
    }

    [Fact]
    public void FansOutTradesAcrossMultipleTimeframes()
    {
        var engine = new StreamingIndicatorEngine(new StreamingIndicatorEngineOptions
        {
            EmitUpdates = false
        });

        var updates = new List<IndicatorUpdate>();
        engine.RegisterIndicator(
            "AAPL",
            new[] { BarTimeframe.Tick, BarTimeframe.Seconds(1) },
            data => data.CalculateSimpleMovingAverage(2),
            update => updates.Add(update),
            new IndicatorSubscriptionOptions { IncludeUpdates = false });

        var start = new DateTime(2024, 1, 2, 9, 30, 0, DateTimeKind.Utc);
        engine.OnTrade(new StreamTrade("AAPL", start, 10, 1));
        engine.OnTrade(new StreamTrade("AAPL", start.AddSeconds(1), 20, 1));
        engine.OnTrade(new StreamTrade("AAPL", start.AddSeconds(2), 30, 1));

        var tickCount = 0;
        var secondCount = 0;
        for (var i = 0; i < updates.Count; i++)
        {
            var update = updates[i];
            if (update.Timeframe.Equals(BarTimeframe.Tick))
            {
                tickCount++;
            }
            else if (update.Timeframe.Equals(BarTimeframe.Seconds(1)))
            {
                secondCount++;
            }
        }

        tickCount.Should().Be(3);
        secondCount.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void RegistersStatefulIndicatorAndEmitsUpdates()
    {
        var engine = new StreamingIndicatorEngine(new StreamingIndicatorEngineOptions
        {
            EmitUpdates = false
        });

        StreamingIndicatorStateUpdate? last = null;
        using var registration = engine.RegisterStatefulIndicator(
            "AAPL",
            BarTimeframe.Tick,
            new SimpleMovingAverageState(2),
            update => last = update,
            new IndicatorSubscriptionOptions { IncludeUpdates = false, IncludeOutputValues = true });

        var start = new DateTime(2024, 1, 2, 9, 30, 0, DateTimeKind.Utc);
        engine.OnTrade(new StreamTrade("AAPL", start, 10, 1));
        engine.OnTrade(new StreamTrade("AAPL", start.AddSeconds(1), 20, 1));

        last.Should().NotBeNull();
        last!.Indicator.Should().Be(IndicatorName.SimpleMovingAverage);
        last.Value.Should().BeApproximately(15, 1e-10);
        last.Outputs.Should().NotBeNull();
        last.Outputs!["Sma"].Should().BeApproximately(15, 1e-10);
        last.IsFinalBar.Should().BeTrue();
    }
}

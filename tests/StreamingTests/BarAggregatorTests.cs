using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.StreamingTests;

public sealed class BarAggregatorTests
{
    [Fact]
    public void AggregatesTradesIntoMinuteBars()
    {
        var options = new BarAggregatorOptions("AAPL", BarTimeframe.Minutes(1))
        {
            EmitUpdates = false
        };
        var aggregator = new BarAggregator(options);
        var closedBars = new List<OhlcvBar>();
        aggregator.BarClosed += bar => closedBars.Add(bar);

        var start = new DateTime(2024, 1, 2, 9, 30, 0, DateTimeKind.Utc);
        aggregator.AddTrade(new StreamTrade("AAPL", start, 10, 1));
        aggregator.AddTrade(new StreamTrade("AAPL", start.AddSeconds(30), 12, 2));
        aggregator.AddTrade(new StreamTrade("AAPL", start.AddSeconds(59), 9, 1));
        aggregator.AddTrade(new StreamTrade("AAPL", start.AddMinutes(1), 11, 3));
        aggregator.Complete();

        closedBars.Count.Should().Be(2);
        var first = closedBars[0];
        first.Open.Should().Be(10);
        first.High.Should().Be(12);
        first.Low.Should().Be(9);
        first.Close.Should().Be(9);
        first.Volume.Should().Be(4);
    }

    [Fact]
    public void EmitsUpdatedBarsWhenEnabled()
    {
        var options = new BarAggregatorOptions("AAPL", BarTimeframe.Seconds(1))
        {
            EmitUpdates = true
        };
        var aggregator = new BarAggregator(options);
        var updates = new List<OhlcvBar>();
        var closes = new List<OhlcvBar>();
        aggregator.BarUpdated += bar => updates.Add(bar);
        aggregator.BarClosed += bar => closes.Add(bar);

        var start = new DateTime(2024, 1, 2, 9, 30, 0, DateTimeKind.Utc);
        aggregator.AddTrade(new StreamTrade("AAPL", start, 10, 1));
        aggregator.AddTrade(new StreamTrade("AAPL", start.AddMilliseconds(200), 11, 1));
        aggregator.AddTrade(new StreamTrade("AAPL", start.AddMilliseconds(400), 9, 1));
        aggregator.AddTrade(new StreamTrade("AAPL", start.AddSeconds(1), 12, 1));
        aggregator.Complete();

        updates.Count.Should().BeGreaterThanOrEqualTo(3);
        closes.Count.Should().Be(2);
        updates[updates.Count - 1].Close.Should().Be(12);
    }

    [Fact]
    public void DropsOutOfOrderTradesWhenConfigured()
    {
        var options = new BarAggregatorOptions("AAPL", BarTimeframe.Seconds(1))
        {
            EmitUpdates = false,
            OutOfOrderPolicy = OutOfOrderPolicy.Drop
        };
        var aggregator = new BarAggregator(options);
        var closedBars = new List<OhlcvBar>();
        aggregator.BarClosed += bar => closedBars.Add(bar);

        var start = new DateTime(2024, 1, 2, 9, 30, 0, DateTimeKind.Utc);
        aggregator.AddTrade(new StreamTrade("AAPL", start.AddSeconds(1), 10, 1));
        aggregator.AddTrade(new StreamTrade("AAPL", start, 20, 1));
        aggregator.Complete();

        closedBars.Count.Should().Be(1);
        closedBars[0].Open.Should().Be(10);
        closedBars[0].High.Should().Be(10);
    }
}

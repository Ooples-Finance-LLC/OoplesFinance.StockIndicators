using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.StreamingTests;

public sealed class StreamingOptionsTests
{
    [Fact]
    public void DefaultTimeframesIncludeStandardSet()
    {
        var options = new StreamingOptions();
        var timeframes = options.GetTimeframes();

        timeframes.Should().Contain(tf => tf.Equals(BarTimeframe.Tick));
        timeframes.Should().Contain(tf => tf.Equals(BarTimeframe.Seconds(1)));
        timeframes.Should().Contain(tf => tf.Equals(BarTimeframe.Seconds(5)));
        timeframes.Should().Contain(tf => tf.Equals(BarTimeframe.Minutes(1)));
        timeframes.Should().Contain(tf => tf.Equals(BarTimeframe.Minutes(5)));
        timeframes.Should().Contain(tf => tf.Equals(BarTimeframe.Minutes(15)));
        timeframes.Should().Contain(tf => tf.Equals(BarTimeframe.Hours(1)));
        timeframes.Should().Contain(tf => tf.Equals(BarTimeframe.Days(1)));
    }

    [Fact]
    public void UpdatePolicyFinalOnlyDisablesUpdates()
    {
        var options = new StreamingOptions { UpdatePolicy = StreamingUpdatePolicy.FinalOnly };

        var engineOptions = options.CreateEngineOptions();
        engineOptions.EmitUpdates.Should().BeFalse();

        var subscriptionOptions = options.CreateSubscriptionOptions();
        subscriptionOptions.IncludeUpdates.Should().BeFalse();
    }

    [Fact]
    public void SubscriptionRequestDefaultsToTradesAndQuotes()
    {
        var options = new StreamingOptions { Symbols = new[] { "AAPL" } };
        var request = options.CreateSubscriptionRequest();

        request.Trades.Should().BeTrue();
        request.Quotes.Should().BeTrue();
        request.Bars.Should().BeFalse();
    }
}

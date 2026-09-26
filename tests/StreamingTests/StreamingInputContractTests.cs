using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.StreamingTests;

public sealed class StreamingInputContractTests
{
    private static readonly DateTime Start = new(2024, 1, 2, 9, 30, 0, DateTimeKind.Utc);

    public static IEnumerable<object[]> InvalidFields()
    {
        foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            foreach (var route in new[] { "bar", "trade", "quote" })
                for (var field = 0; field < (route == "bar" ? 5 : route == "trade" ? 2 : 4); field++)
                    yield return new object[] { route, field, value };
    }

    [Theory]
    [MemberData(nameof(InvalidFields))]
    public void EngineRejectsBeforeAnySubscriptionAdvances(string route, int field, double invalid)
    {
        var engine = new StreamingIndicatorEngine(new() { EmitUpdates = false });
        var batch = new List<double>();
        var native = new List<double>();
        using var batchRegistration = engine.RegisterIndicator("AAPL", BarTimeframe.Tick,
            data => data.CalculateSimpleMovingAverage(2), update => batch.Add(update.IndicatorData.CustomValuesList[^1]));
        using var nativeRegistration = engine.RegisterStatefulIndicator("AAPL", BarTimeframe.Tick,
            new SimpleMovingAverageState(2), update => native.Add(update.Value));
        Send(10, null);
        Assert.Throws<ArgumentOutOfRangeException>(() => Send(999, invalid));
        Assert.Single(batch);
        Assert.Single(native);
        Send(20, null);
        Assert.Equal(new[] { 0d, 15d }, batch); // SMA emits zero until the full window is available.
        Assert.Equal(batch, native);

        void Send(double price, double? bad)
        {
            double Value(int index, double valid) => bad.HasValue && field == index ? bad.Value : valid;
            var time = Start.AddSeconds(price == 10 ? 0 : 1);
            switch (route)
            {
                case "bar":
                    engine.OnBar(new("AAPL", BarTimeframe.Tick, time, time,
                        Value(0, price), Value(1, price), Value(2, price), Value(3, price), Value(4, 1), true));
                    break;
                case "trade": engine.OnTrade(new("AAPL", time, Value(0, price), Value(1, 1))); break;
                default: engine.OnQuote(new("AAPL", time, Value(0, price), Value(1, price), Value(2, 1), Value(3, 1))); break;
            }
        }
    }

    public static IEnumerable<object[]> InvalidAggregatorFields() => InvalidFields().Where(row => (string)row[0] != "bar");

    [Theory]
    [MemberData(nameof(InvalidAggregatorFields))]
    public void DirectAggregatorRejectsBeforeBuffering(string route, int field, double invalid)
    {
        var aggregator = new BarAggregator(new("AAPL", BarTimeframe.Minutes(1))
        { EmitUpdates = false, OutOfOrderPolicy = OutOfOrderPolicy.BufferWithinWindow, ReorderWindow = TimeSpan.FromSeconds(5) });
        var bars = new List<OhlcvBar>();
        aggregator.BarClosed += bars.Add;
        aggregator.AddSample("AAPL", Start, 10, 1);
        double Value(int index, double valid) => field == index ? invalid : valid;
        // A far-future invalid timestamp must not flush pending valid samples or poison the watermark.
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            if (route == "trade") aggregator.AddTrade(new("AAPL", Start.AddDays(1), Value(0, 999), Value(1, 1)));
            else aggregator.AddQuote(new("AAPL", Start.AddDays(1), Value(0, 999), Value(1, 999), Value(2, 1), Value(3, 1)));
        });
        Assert.Empty(bars);
        aggregator.AddSample("AAPL", Start.AddSeconds(1), 20, 1);
        aggregator.Complete();
        var bar = Assert.Single(bars);
        Assert.Equal(10, bar.Open);
        Assert.Equal(20, bar.High);
        Assert.Equal(10, bar.Low);
        Assert.Equal(20, bar.Close);
        Assert.Equal(2, bar.Volume);
    }

    [Theory]
    [InlineData(double.MaxValue)]
    [InlineData(-double.MaxValue)]
    [InlineData(double.Epsilon)]
    [InlineData(-double.Epsilon)]
    [InlineData(0d)]
    public void QuoteMidpointsPreserveFiniteExtremeAndSubnormalValues(double value)
    {
        var aggregator = new BarAggregator(new("AAPL", BarTimeframe.Tick) { EmitUpdates = false });
        OhlcvBar? result = null;
        aggregator.BarClosed += bar => result = bar;
        aggregator.AddQuote(new("AAPL", Start, value, value, value, value));
        Assert.NotNull(result);
        Assert.Equal(value, result.Close);
        Assert.Equal(value, result.Volume);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DirectSamplesCannotBufferNonfiniteFields(bool price)
    {
        var aggregator = new BarAggregator(new("AAPL", BarTimeframe.Tick));
        Assert.Throws<ArgumentOutOfRangeException>(() => aggregator.AddSample("AAPL", Start,
            price ? double.NaN : 1, price ? 1 : double.PositiveInfinity));
    }
}

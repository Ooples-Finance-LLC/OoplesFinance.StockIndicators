using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.StreamingTests;

public sealed class StreamingInputSelectorTests
{
    [Fact]
    public void ReturnsTypicalPrice()
    {
        var bar = new OhlcvBar("AAPL", BarTimeframe.Tick, new DateTime(2024, 1, 2), new DateTime(2024, 1, 2),
            open: 100, high: 110, low: 90, close: 105, volume: 1, isFinal: true);

        var value = StreamingInputSelector.GetValue(bar, InputName.TypicalPrice);
        value.Should().BeApproximately((110 + 90 + 105) / 3.0, 1e-10);
    }

    [Fact]
    public void ThrowsForMidpointInputs()
    {
        var bar = new OhlcvBar("AAPL", BarTimeframe.Tick, new DateTime(2024, 1, 2), new DateTime(2024, 1, 2),
            open: 100, high: 110, low: 90, close: 105, volume: 1, isFinal: true);

        Action action = () => StreamingInputSelector.GetValue(bar, InputName.Midpoint);
        action.Should().Throw<NotSupportedException>();
    }
}

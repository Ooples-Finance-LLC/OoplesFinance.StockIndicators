namespace OoplesFinance.StockIndicators.Tests.Unit.CalculationsTests;

public sealed class EhlersTests : GlobalTestData
{
    [Fact]
    public void CalculateEhlersMotherOfAdaptiveMovingAverages_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);

        // Act
        var results = stockData.CalculateEhlersMotherOfAdaptiveMovingAverages().CustomValuesList;

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Count.Should().Be(stockData.Count);
    }
}

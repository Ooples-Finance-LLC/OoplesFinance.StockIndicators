using System.Reflection;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class StockDataLazyViewTests
{
    [Fact]
    public void ColumnConstructor_DefersRowViewUntilAccess()
    {
        var stockData = new StockData(
            new[] { 1d, 2d },
            new[] { 3d, 4d },
            new[] { 0.5d, 1.5d },
            new[] { 2d, 3d },
            new[] { 10d, 20d },
            new[] { new DateTime(2024, 1, 1), new DateTime(2024, 1, 2) });

        var field = GetPrivateField(stockData, "_tickerDataList");
        field.GetValue(stockData).Should().BeNull();

        var rows = stockData.TickerDataList;

        rows.Count.Should().Be(2);
        field.GetValue(stockData).Should().BeSameAs(rows);
    }

    [Fact]
    public void RowConstructor_DefersColumnsUntilAccess()
    {
        var rows = new List<TickerData>
        {
            new() { Date = new DateTime(2024, 1, 1), Open = 1, High = 3, Low = 0.5, Close = 2, Volume = 10 },
            new() { Date = new DateTime(2024, 1, 2), Open = 2, High = 4, Low = 1.5, Close = 3, Volume = 20 }
        };
        var stockData = new StockData(rows);

        var field = GetPrivateField(stockData, "_openPrices");
        field.GetValue(stockData).Should().BeNull();

        var opens = stockData.OpenPrices;

        opens.Should().Equal(1d, 2d);
        field.GetValue(stockData).Should().BeSameAs(opens);
    }

    [Fact]
    public void RowAndColumnViewsAreCached()
    {
        var stockData = new StockData(
            new[] { 1d },
            new[] { 2d },
            new[] { 0.5d },
            new[] { 1.5d },
            new[] { 10d },
            new[] { new DateTime(2024, 1, 1) });

        var rows = stockData.TickerDataList;
        var rowsAgain = stockData.TickerDataList;
        ReferenceEquals(rows, rowsAgain).Should().BeTrue();

        var closes = stockData.ClosePrices;
        var closesAgain = stockData.ClosePrices;
        ReferenceEquals(closes, closesAgain).Should().BeTrue();
    }

    private static FieldInfo GetPrivateField(StockData stockData, string name)
    {
        var field = typeof(StockData).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull();
        return field!;
    }
}

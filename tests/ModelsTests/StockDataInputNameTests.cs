namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

/// <summary>
/// The <see cref="InputName"/> a <see cref="StockData"/> is built with is the series its indicators
/// read (#182).
/// </summary>
/// <remarks>
/// Each test compares against the path that has always worked - handing the same series in through
/// <see cref="StockData.InputValues"/>, which the single-argument <c>GetInputValuesList</c> reads -
/// rather than against a hand-rolled calculation, so they pin where the series comes from and nothing
/// about how any one indicator computes.
/// </remarks>
public sealed class StockDataInputNameTests : GlobalTestData
{
    private const int Bars = 200;

    private List<TickerData> Tickers() => StockTestData.Take(Bars).ToList();

    private static List<double> Median(IEnumerable<TickerData> tickers) =>
        tickers.Select(t => (t.High + t.Low) / 2).ToList();

    private static List<double> Typical(IEnumerable<TickerData> tickers) =>
        tickers.Select(t => (t.High + t.Low + t.Close) / 3).ToList();

    [Fact]
    public void TheInputNameGivenToTheConstructorIsTheSeriesIndicatorsRead()
    {
        var tickers = Tickers();

        var viaInputName = new StockData(tickers, InputName.MedianPrice)
            .CalculateSimpleMovingAverage(20).CustomValuesList;
        var reference = new StockData(tickers) { InputValues = Median(tickers) }
            .CalculateSimpleMovingAverage(20).CustomValuesList;
        var onClose = new StockData(tickers).CalculateSimpleMovingAverage(20).CustomValuesList;

        viaInputName.Should().Equal(reference);
        viaInputName.Should().NotEqual(onClose,
            "median price and close are different series, so the result must not be the close-based one");
    }

    [Fact]
    public void TheColumnConstructorHonoursItToo()
    {
        var tickers = Tickers();
        var data = new StockData(
            tickers.Select(t => t.Open), tickers.Select(t => t.High), tickers.Select(t => t.Low),
            tickers.Select(t => t.Close), tickers.Select(t => t.Volume), tickers.Select(t => t.Date),
            InputName.TypicalPrice);

        var viaInputName = data.CalculateSimpleMovingAverage(20).CustomValuesList;
        var reference = new StockData(tickers) { InputValues = Typical(tickers) }
            .CalculateSimpleMovingAverage(20).CustomValuesList;

        viaInputName.Should().Equal(reference);
    }

    [Fact]
    public void AChainedSeriesStillWinsAfterTheFirstLink()
    {
        var tickers = Tickers();

        var chained = new StockData(tickers, InputName.MedianPrice)
            .CalculateExponentialMovingAverage(10)
            .CalculateSimpleMovingAverage(20).CustomValuesList;
        var reference = new StockData(tickers) { InputValues = Median(tickers) }
            .CalculateExponentialMovingAverage(10)
            .CalculateSimpleMovingAverage(20).CustomValuesList;

        chained.Should().Equal(reference,
            "InputName decides what the first indicator reads; the second reads the first one's output");
    }

    [Fact]
    public void ChangingTheInputNameAfterConstructionRebuildsTheSeries()
    {
        var tickers = Tickers();
        var data = new StockData(tickers);

        data.InputValues.Should().Equal(data.ClosePrices);

        data.InputName = InputName.MedianPrice;

        data.InputValues.Should().Equal(Median(tickers),
            "the cached close series was built from the previous name and must not outlive it");
    }

    [Fact]
    public void AnExplicitlyAssignedSeriesSurvivesAnInputNameChange()
    {
        var tickers = Tickers();
        var mine = Enumerable.Range(0, tickers.Count).Select(i => (double)i).ToList();
        var data = new StockData(tickers) { InputValues = mine };

        data.InputName = InputName.High;

        data.InputValues.Should().Equal(mine, "the caller's own series is not the InputName's to replace");
    }

    [Theory]
    [InlineData(InputName.Midpoint)]
    [InlineData(InputName.Midprice)]
    public void BuildingAnIndicatorBackedSeriesDoesNotWriteOntoTheObject(InputName inputName)
    {
        var tickers = Tickers();
        var data = new StockData(tickers, inputName);
        var indicatorBefore = data.IndicatorName;

        data.InputValues.Should().HaveCount(tickers.Count);

        data.CustomValuesList.Should().BeEmpty("reading a property must not chain an indicator onto the data");
        data.OutputValues.Should().BeEmpty();
        data.IndicatorName.Should().Be(indicatorBefore);
    }
}

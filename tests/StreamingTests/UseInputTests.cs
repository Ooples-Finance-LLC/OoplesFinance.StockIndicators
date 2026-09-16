using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.StreamingTests;

/// <summary>
/// <c>StockData.UseInput</c>: the batch replacement for passing an input name, sharing its presets with
/// streaming.
/// </summary>
public sealed class UseInputTests : GlobalTestData
{
    private const int Bars = 200;
    private const double Tolerance = 1e-9;

    private List<TickerData> Tickers() => StockTestData.Take(Bars).ToList();

    [Fact]
    public void APresetGivesExactlyWhatChainingTheIndicatorItNamesGives()
    {
        var tickers = Tickers();

        var viaPreset = new StockData(tickers).UseInput(InputSeries.MedianPrice)
            .CalculateSimpleMovingAverage(20).CustomValuesList;
        var viaChain = new StockData(tickers).CalculateMedianPrice()
            .CalculateSimpleMovingAverage(20).CustomValuesList;

        viaPreset.Should().Equal(viaChain);
    }

    [Fact]
    public void AWindowedPresetMatchesChainingItsIndicator()
    {
        var tickers = Tickers();

        var viaPreset = new StockData(tickers).UseInput(InputSeries.Midpoint(14))
            .CalculateSimpleMovingAverage(20).CustomValuesList;
        var viaChain = new StockData(tickers).CalculateMidpoint(14)
            .CalculateSimpleMovingAverage(20).CustomValuesList;

        viaPreset.Should().HaveCount(viaChain.Count);
        for (var i = 0; i < viaChain.Count; i++)
        {
            viaPreset[i].Should().BeApproximately(viaChain[i], Tolerance, $"bar {i}");
        }
    }

    /// <summary>The point of sharing presets: one object, the same numbers in either engine.</summary>
    [Fact]
    public void BothEnginesAgreeOnTheSamePreset()
    {
        var tickers = Tickers();

        var batch = new StockData(tickers).UseInput(InputSeries.TypicalPrice)
            .CalculateSimpleMovingAverage(20).CustomValuesList;

        var state = new CustomInputState(new SimpleMovingAverageState(20), InputSeries.TypicalPrice);
        var streamed = tickers.Select(t => state.Update(
            new OhlcvBar("TEST", BarTimeframe.Tick, t.Date, t.Date, t.Open, t.High, t.Low, t.Close, t.Volume, isFinal: true),
            isFinal: true, includeOutputs: false).Value).ToList();

        streamed.Should().HaveCount(batch.Count);
        for (var i = 0; i < batch.Count; i++)
        {
            streamed[i].Should().BeApproximately(batch[i], Tolerance, $"bar {i}");
        }
    }

    [Fact]
    public void AStatefulSeriesStartsFreshEachTimeItIsUsed()
    {
        var tickers = Tickers();
        var midpoint = InputSeries.Midpoint(14);

        var first = new StockData(tickers).UseInput(midpoint).CustomValuesList;
        var second = new StockData(tickers).UseInput(midpoint).CustomValuesList;

        second.Should().Equal(first, "UseInput resets the series, so reusing it does not continue the old window");
    }

    /// <summary>
    /// IncludeCustomValues decides whether an indicator's OUTPUT is published; it must not decide whether the
    /// caller's input reaches the indicator. It used to: the series was cleared and the SMA ran on the close.
    /// </summary>
    [Fact]
    public void TheInputIsUsedWhenCustomValuesAreNotPublished()
    {
        var tickers = Tickers();

        var published = new StockData(tickers).UseInput(InputSeries.MedianPrice).CalculateSimpleMovingAverage(20);
        var unpublished = new StockData(tickers) { Options = new IndicatorOptions { IncludeCustomValues = false } }
            .UseInput(InputSeries.MedianPrice).CalculateSimpleMovingAverage(20);
        var onClose = new StockData(tickers).CalculateSimpleMovingAverage(20);

        unpublished.OutputValues.Should().BeEquivalentTo(published.OutputValues);
        unpublished.OutputValues.Should().NotBeEquivalentTo(onClose.OutputValues,
            "the control: if the input were dropped this is what the indicator would compute");
    }

    /// <summary>
    /// A chain computes the same with its series unpublished. Each calculation returns the same StockData, so the
    /// next one reads what the last left behind; with IncludeCustomValues off that used to be an emptied list,
    /// and the RSI ran on the close instead of the average.
    /// </summary>
    [Fact]
    public void AChainComputesTheSameWhenCustomValuesAreNotPublished()
    {
        var tickers = Tickers();

        var published = new StockData(tickers).CalculateSimpleMovingAverage(20).CalculateRelativeStrengthIndex();
        var unpublished = new StockData(tickers) { Options = new IndicatorOptions { IncludeCustomValues = false } }
            .CalculateSimpleMovingAverage(20).CalculateRelativeStrengthIndex();
        var onClose = new StockData(tickers).CalculateRelativeStrengthIndex();

        unpublished.OutputValues.Should().BeEquivalentTo(published.OutputValues);
        unpublished.OutputValues.Should().NotBeEquivalentTo(onClose.OutputValues,
            "the control: if the average were dropped this is what the RSI would compute");
        unpublished.CustomValuesList.Should().BeEmpty("the option still hides the series from the caller");
    }

    /// <summary>RoundingDigits rounds what an indicator publishes, never what it computes on.</summary>
    [Fact]
    public void TheInputIsNotRoundedByTheOutputRounding()
    {
        var tickers = Tickers();

        var input = new StockData(tickers) { Options = new IndicatorOptions { RoundingDigits = 2 } }
            .UseInput(InputSeries.Of(bar => bar.Close / 3)).CustomValuesList;

        input.Should().Equal(tickers.Select(t => t.Close / 3));
        input.Should().Contain(v => v != Math.Round(v, 2), "the control: a third of a price is not a whole cent");
    }
}

using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.StreamingTests;

/// <summary>
/// The input-series presets that replace passing an input name, and streaming chaining.
/// </summary>
public sealed class InputSeriesTests : GlobalTestData
{
    private const int Bars = 200;
    private const double Tolerance = 1e-9;

    private List<TickerData> Tickers() => StockTestData.Take(Bars).ToList();

    private static OhlcvBar ToBar(TickerData t, bool isFinal = true) =>
        new("TEST", BarTimeframe.Tick, t.Date, t.Date, t.Open, t.High, t.Low, t.Close, t.Volume, isFinal);

    /// <summary>The migration table: each preset and the input name it replaces.</summary>
    public static IEnumerable<object[]> Presets()
    {
        yield return new object[] { nameof(InputName.Close), nameof(InputSeries.Close) };
        yield return new object[] { nameof(InputName.AdjustedClose), nameof(InputSeries.AdjustedClose) };
        yield return new object[] { nameof(InputName.Open), nameof(InputSeries.Open) };
        yield return new object[] { nameof(InputName.High), nameof(InputSeries.High) };
        yield return new object[] { nameof(InputName.Low), nameof(InputSeries.Low) };
        yield return new object[] { nameof(InputName.Volume), nameof(InputSeries.Volume) };
        yield return new object[] { nameof(InputName.MedianPrice), nameof(InputSeries.MedianPrice) };
        yield return new object[] { nameof(InputName.TypicalPrice), nameof(InputSeries.TypicalPrice) };
        yield return new object[] { nameof(InputName.FullTypicalPrice), nameof(InputSeries.FullTypicalPrice) };
        yield return new object[] { nameof(InputName.WeightedClose), nameof(InputSeries.WeightedClose) };
        yield return new object[] { nameof(InputName.AveragePrice), nameof(InputSeries.AveragePrice) };
    }

    /// <summary>
    /// A one-token migration only works if the preset is exactly the value the name selected - not
    /// approximately, and not the same formula re-typed with a different rounding order.
    /// </summary>
    [Theory]
    [MemberData(nameof(Presets))]
    public void EachPresetIsExactlyTheInputNameItReplaces(string inputName, string preset)
    {
        // The row carries the name as a string: InputName is internal now, and a public test method
        // cannot expose an internal type in its signature.
        var name = Enum.Parse<InputName>(inputName);
        var series = (IInputSeries)typeof(InputSeries).GetProperty(preset)!.GetValue(null)!;

        foreach (var ticker in Tickers())
        {
            var bar = ToBar(ticker);
            series.Next(bar, isFinal: true).Should().Be(StreamingInputSelector.GetValue(bar, name),
                $"{preset} replaces InputName.{name}");
        }
    }

    [Fact]
    public void TheMidpointPresetMatchesTheBatchMidpoint()
    {
        var tickers = Tickers();
        var series = InputSeries.Midpoint(14);

        var streamed = tickers.Select(t => series.Next(ToBar(t), isFinal: true)).ToList();
        var batch = new StockData(tickers).CalculateMidpoint(14).CustomValuesList;

        streamed.Should().HaveCount(batch.Count);
        for (var i = 0; i < batch.Count; i++)
        {
            streamed[i].Should().BeApproximately(batch[i], Tolerance, $"bar {i}");
        }
    }

    [Fact]
    public void TheMidpricePresetMatchesTheBatchMidprice()
    {
        var tickers = Tickers();
        var series = InputSeries.Midprice(14);

        var streamed = tickers.Select(t => series.Next(ToBar(t), isFinal: true)).ToList();
        var batch = new StockData(tickers).CalculateMidprice(14).CustomValuesList;

        streamed.Should().HaveCount(batch.Count);
        for (var i = 0; i < batch.Count; i++)
        {
            streamed[i].Should().BeApproximately(batch[i], Tolerance, $"bar {i}");
        }
    }

    /// <summary>
    /// The reason a windowed series is an interface rather than a function: revising a forming bar any
    /// number of times must give the same final answer as seeing it once.
    /// </summary>
    [Fact]
    public void AFormingBarDoesNotAdvanceAWindowedSeries()
    {
        var tickers = Tickers();
        var revised = InputSeries.Midpoint(14);
        var clean = InputSeries.Midpoint(14);

        foreach (var t in tickers)
        {
            // Two revisions of the forming bar, one well away from the final values.
            revised.Next(new OhlcvBar("TEST", BarTimeframe.Tick, t.Date, t.Date, t.Open, t.High * 2, t.Low / 2,
                t.Close * 3, t.Volume, isFinal: false), isFinal: false);
            revised.Next(ToBar(t, isFinal: false), isFinal: false);

            revised.Next(ToBar(t), isFinal: true).Should().Be(clean.Next(ToBar(t), isFinal: true));
        }
    }

    /// <summary>
    /// Streaming chaining is the counterpart of batch chaining, so the two must give the same numbers.
    /// </summary>
    [Fact]
    public void AStateAsTheInputMatchesBatchChaining()
    {
        var tickers = Tickers();

        var chained = new CustomInputState(new SimpleMovingAverageState(20), InputSeries.Of(new RelativeStrengthIndexState(14)));
        var streamed = tickers.Select(t => chained.Update(ToBar(t), isFinal: true, includeOutputs: false).Value).ToList();

        var batch = new StockData(tickers).CalculateRelativeStrengthIndex(length: 14).CalculateSimpleMovingAverage(20).CustomValuesList;

        streamed.Should().HaveCount(batch.Count);
        for (var i = 0; i < batch.Count; i++)
        {
            streamed[i].Should().BeApproximately(batch[i], Tolerance, $"bar {i}");
        }
    }

    [Fact]
    public void APresetGivesTheSameResultAsTheSelectorItReplaces()
    {
        var tickers = Tickers();

        var viaPreset = new CustomInputState(new SimpleMovingAverageState(20), InputSeries.MedianPrice);
        var viaSelector = new CustomInputState(new SimpleMovingAverageState(20), bar => (bar.High + bar.Low) / 2);

        foreach (var t in tickers)
        {
            viaPreset.Update(ToBar(t), true, false).Value.Should().Be(viaSelector.Update(ToBar(t), true, false).Value);
        }
    }
}

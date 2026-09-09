using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

/// <summary>
/// Phase 0 of the #145 work: an explicit input series, and derived helpers that take it as a
/// parameter instead of reading it off a shared <see cref="StockData"/>.
/// </summary>
public sealed class IndicatorSourceTests : GlobalTestData
{
    private const int SampleSize = 200;
    private const double Tolerance = 1e-10;

    private static StockData CreateData() => new(StockTestData.Take(SampleSize));

    // ---------------------------------------------------------------------------------------------
    // Resolve applies the rule GetInputValuesList already applies, so chaining is unaffected.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Resolve_WithoutCustomValues_UsesInputValues()
    {
        var data = CreateData();

        var source = IndicatorSource.Resolve(data);

        source.Values.Should().BeSameAs(data.InputValues);
        source.Count.Should().Be(data.InputValues.Count);
    }

    [Fact]
    public void Resolve_WithCustomValues_UsesTheChainedSeries()
    {
        var data = CreateData();
        var chained = new List<double>(Enumerable.Range(0, data.Count).Select(i => (double)i));
        data.SetCustomValues(chained);

        var source = IndicatorSource.Resolve(data);

        source.Values.Should().BeSameAs(chained,
            "a chained series must still arrive as the calculation input - Resolve changes when it is "
            + "read, not what is read");
    }

    [Fact]
    public void Resolve_CarriesTheBarsAlongsideTheInputSeries()
    {
        var data = CreateData();

        var source = IndicatorSource.Resolve(data);

        source.High.Should().BeSameAs(data.HighPrices);
        source.Low.Should().BeSameAs(data.LowPrices);
        source.Open.Should().BeSameAs(data.OpenPrices);
        source.Volume.Should().BeSameAs(data.Volumes);
    }

    [Fact]
    public void Resolve_IsAnUnchangingSnapshot()
    {
        var data = CreateData();
        var source = IndicatorSource.Resolve(data);
        var before = source.Values;

        // Exactly what GetMovingAverageList does to the caller's object mid-calculation.
        data.SetCustomValues(new List<double>(Enumerable.Repeat(1.0, data.Count)));

        source.Values.Should().BeSameAs(before,
            "a resolved source is a snapshot - this is what makes the #145 defect unrepresentable");
    }

    // ---------------------------------------------------------------------------------------------
    // WithValues: explicit chaining that leaves the caller's StockData alone.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void WithValues_DoesNotMutateTheOriginal()
    {
        var data = CreateData();
        var series = new List<double>(Enumerable.Repeat(42.0, data.Count));

        var view = data.WithValues(series);

        view.CustomValuesList.Should().BeSameAs(series);
        data.CustomValuesList.Should().BeEmpty("the original must be untouched");
    }

    [Fact]
    public void WithValues_SharesTheBarsRatherThanCopyingThem()
    {
        var data = CreateData();

        var view = data.WithValues(new List<double>(Enumerable.Repeat(1.0, data.Count)));

        view.HighPrices.Should().BeSameAs(data.HighPrices);
        view.LowPrices.Should().BeSameAs(data.LowPrices);
        view.ClosePrices.Should().BeSameAs(data.ClosePrices);
        view.Count.Should().Be(data.Count);
    }

    [Fact]
    public void WithValues_ProducesTheSameResultAsImplicitChaining()
    {
        var implicitChain = CreateData().CalculateSimpleMovingAverage(20).CalculateBollingerBands()
            .OutputValues["MiddleBand"];

        var source = CreateData();
        var sma = CreateData().CalculateSimpleMovingAverage(20).CustomValuesList;
        var explicitChain = source.WithValues(sma).CalculateBollingerBands().OutputValues["MiddleBand"];

        explicitChain.Should().HaveCount(implicitChain.Count);
        for (var i = 0; i < implicitChain.Count; i++)
        {
            explicitChain[i].Should().BeApproximately(implicitChain[i], Tolerance,
                $"explicit chaining must reproduce implicit chaining exactly, index {i}");
        }
    }

    // ---------------------------------------------------------------------------------------------
    // The derived helpers are faithful ports: identical results when nothing has poisoned the input.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void StandardDeviation_MatchesTheExistingCalculation_OnCleanInput()
    {
        var expected = CreateData().CalculateStandardDeviationVolatility(MovingAvgType.SimpleMovingAverage, 20)
            .CustomValuesList;

        var data = CreateData();
        var actual = IndicatorMath.StandardDeviation(data, IndicatorSource.Resolve(data),
            MovingAvgType.SimpleMovingAverage, 20);

        actual.Should().HaveCount(expected.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            actual[i].Should().BeApproximately(expected[i], Tolerance, $"index {i}");
        }
    }

    [Fact]
    public void StandardDeviation_MatchesTheExistingCalculation_ForANonSimpleAverage()
    {
        var expected = CreateData().CalculateStandardDeviationVolatility(MovingAvgType.ExponentialMovingAverage, 20)
            .CustomValuesList;

        var data = CreateData();
        var actual = IndicatorMath.StandardDeviation(data, IndicatorSource.Resolve(data),
            MovingAvgType.ExponentialMovingAverage, 20);

        actual.Should().HaveCount(expected.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            actual[i].Should().BeApproximately(expected[i], Tolerance, $"index {i}");
        }
    }

    [Fact]
    public void AverageTrueRange_MatchesTheExistingCalculation_OnCleanInput()
    {
        var expected = CreateData().CalculateAverageTrueRange(MovingAvgType.WildersSmoothingMethod, 14)
            .CustomValuesList;

        var data = CreateData();
        var actual = IndicatorMath.AverageTrueRange(data, IndicatorSource.Resolve(data),
            MovingAvgType.WildersSmoothingMethod, 14);

        actual.Should().HaveCount(expected.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            actual[i].Should().BeApproximately(expected[i], Tolerance, $"index {i}");
        }
    }

    [Fact]
    public void TrueRange_UsesTheBarsOwnCloseForTheFirstBar()
    {
        var data = CreateData();
        var source = IndicatorSource.Resolve(data);

        var trueRange = IndicatorMath.TrueRange(source);

        trueRange[0].Should().BeApproximately(source.High[0] - source.Low[0], Tolerance,
            "with no previous bar the true range is the bar's range - the rule #146 corrected the "
            + "streaming states to");
    }

    // ---------------------------------------------------------------------------------------------
    // The point of the exercise: the helpers cannot be poisoned by a preceding moving average.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MovingAverage_DoesNotWriteToTheCallersCustomValues()
    {
        var data = CreateData();
        var source = IndicatorSource.Resolve(data);

        IndicatorMath.MovingAverage(data, source, MovingAvgType.SimpleMovingAverage, 20);

        data.CustomValuesList.Should().BeEmpty(
            "GetMovingAverageList publishes onto whatever StockData it is given; IndicatorMath gives it "
            + "a view so the caller's series survives");
    }

    [Fact]
    public void StandardDeviationAfterAMovingAverage_StillMeasuresAgainstPrice()
    {
        var data = CreateData();
        var source = IndicatorSource.Resolve(data);

        // The #145 shape: average first, then measure dispersion on the same object.
        var sma = IndicatorMath.MovingAverage(data, source, MovingAvgType.SimpleMovingAverage, 20);
        var afterAverage = IndicatorMath.StandardDeviation(data, source, MovingAvgType.SimpleMovingAverage, 20);

        // The same call with the average never having happened.
        var clean = CreateData();
        var expected = IndicatorMath.StandardDeviation(clean, IndicatorSource.Resolve(clean),
            MovingAvgType.SimpleMovingAverage, 20);

        sma.Should().NotBeEmpty();
        for (var i = 0; i < expected.Count; i++)
        {
            afterAverage[i].Should().BeApproximately(expected[i], Tolerance,
                $"a preceding moving average must not change what the standard deviation reads, index {i}");
        }
    }

    [Fact]
    public void ReorderingTheCallsChangesNothing()
    {
        var data = CreateData();
        var source = IndicatorSource.Resolve(data);

        var stdDevFirst = IndicatorMath.StandardDeviation(data, source, MovingAvgType.SimpleMovingAverage, 20);
        IndicatorMath.MovingAverage(data, source, MovingAvgType.SimpleMovingAverage, 20);

        var other = CreateData();
        var otherSource = IndicatorSource.Resolve(other);
        IndicatorMath.MovingAverage(other, otherSource, MovingAvgType.SimpleMovingAverage, 20);
        var stdDevSecond = IndicatorMath.StandardDeviation(other, otherSource, MovingAvgType.SimpleMovingAverage, 20);

        for (var i = 0; i < stdDevFirst.Count; i++)
        {
            stdDevSecond[i].Should().BeApproximately(stdDevFirst[i], Tolerance,
                $"call order is exactly what decides the result today, index {i}");
        }
    }

    /// <summary>
    /// The defect this design removes, pinned so the difference is visible rather than asserted.
    /// </summary>
    [Fact]
    public void TheExistingAmbientPathMeasuresDispersionAgainstItsOwnAverage()
    {
        var data = CreateData();
        var (inputList, _, _, _, _) = CalculationsHelper.GetInputValuesList(data);

        // Precisely what CalculateBollingerBands does today.
        CalculationsHelper.GetMovingAverageList(data, MovingAvgType.SimpleMovingAverage, 20, inputList);
        var poisoned = data.CalculateStandardDeviationVolatility(MovingAvgType.SimpleMovingAverage, 20)
            .CustomValuesList;

        var clean = CreateData().CalculateStandardDeviationVolatility(MovingAvgType.SimpleMovingAverage, 20)
            .CustomValuesList;

        poisoned.Should().HaveCount(clean.Count);
        poisoned.Should().NotEqual(clean,
            "this is issue #145: the moving average redefined the series the standard deviation reads, "
            + "so it measures the spread of the average instead of the spread of price");
    }
}

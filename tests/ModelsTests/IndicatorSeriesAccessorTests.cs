using OoplesFinance.StockIndicators.Exceptions;
using OoplesFinance.StockIndicators.Series;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

/// <summary>
/// The generated accessors that let a chain continue from a named output with the name checked by the
/// compiler.
/// </summary>
public sealed class IndicatorSeriesAccessorTests : GlobalTestData
{
    private const int SampleSize = 200;
    private const double Tolerance = 1e-10;

    private static StockData CreateData() => new(StockTestData.Take(SampleSize));

    [Fact]
    public void Accessor_IsTheSameAsAskingForTheOutputByName()
    {
        var bands = CreateData().CalculateBollingerBands();

        var viaAccessor = bands.UpperBand();
        var viaName = bands.SeriesView("UpperBand");

        viaAccessor.CustomValuesList.Should().Equal(viaName.CustomValuesList);
        viaAccessor.CustomValuesList.Should().BeSameAs(bands.OutputValues["UpperBand"]);
    }

    [Fact]
    public void Accessor_LetsAnyCalculationContinueFromANamedOutput()
    {
        var bands = CreateData().CalculateBollingerBands();

        var upperRsi = bands.UpperBand().CalculateRelativeStrengthIndex(length: 14).CustomValuesList;
        var lowerRsi = bands.LowerBand().CalculateRelativeStrengthIndex(length: 14).CustomValuesList;

        upperRsi.Should().NotBeEmpty();
        upperRsi.Should().NotEqual(lowerRsi, "the two bands are different series");
    }

    [Fact]
    public void Accessor_DoesNotDisturbTheResultItCameFrom()
    {
        var data = CreateData();
        var bands = data.CalculateBollingerBands();
        var upperBefore = new List<double>(bands.OutputValues["UpperBand"]);
        var middleBefore = new List<double>(bands.OutputValues["MiddleBand"]);

        bands.UpperBand().CalculateRelativeStrengthIndex(length: 14);
        bands.MiddleBand().CalculateSimpleMovingAverage(10);

        bands.OutputValues["UpperBand"].Should().Equal(upperBefore);
        bands.OutputValues["MiddleBand"].Should().Equal(middleBefore);
        data.CustomValuesList.Should().BeEmpty();
    }

    [Fact]
    public void Accessor_ChainsSeveralTimes()
    {
        var bands = CreateData().CalculateBollingerBands();

        var smaOfUpper = bands.UpperBand().CalculateSimpleMovingAverage(10).CustomValuesList;
        var expected = CreateData().WithValues(bands.OutputValues["UpperBand"])
            .CalculateSimpleMovingAverage(10).CustomValuesList;

        smaOfUpper.Should().HaveCount(expected.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            smaOfUpper[i].Should().BeApproximately(expected[i], Tolerance, $"index {i}");
        }
    }

    /// <summary>
    /// The accessors are read out of the SetOutputValues calls, not from a list kept beside them.
    /// </summary>
    /// <remarks>
    /// The catalog generator carries a hand-written map that records AlligatorIndex as publishing
    /// "Jaw"; the calculation publishes "Jaws". Because these accessors come from the calculation, the
    /// one that exists is <c>Jaws()</c> - there is no <c>Jaw()</c> to compile against. That is the
    /// difference between a name that can drift and one that cannot.
    /// </remarks>
    [Fact]
    public void Accessors_ComeFromTheCalculationsRatherThanAListKeptBesideThem()
    {
        var alligator = CreateData().CalculateAlligatorIndex();

        var jaws = alligator.Jaws();

        jaws.CustomValuesList.Should().BeSameAs(alligator.OutputValues["Jaws"]);
        alligator.OutputValues.Should().ContainKeys("Jaws", "Teeth", "Lips");
        alligator.OutputValues.Should().NotContainKey("Jaw",
            "the hand-written MultiOutputIndicators map in the catalog generator says Jaw, and it is "
            + "wrong - which is the reason these are generated from the source");
    }

    [Fact]
    public void Accessor_ForAnOutputThisResultDoesNotPublish_SaysWhatIsAvailable()
    {
        var bands = CreateData().CalculateBollingerBands();

        // Histogram is a real output name elsewhere in the library, so the accessor compiles - it is
        // simply not something Bollinger Bands publishes.
        var act = () => bands.Histogram();

        act.Should().Throw<CalculationException>()
            .WithMessage("*Histogram*")
            .WithMessage("*UpperBand*");
    }
}

using OoplesFinance.StockIndicators.Exceptions;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Series;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

/// <summary>
/// What someone writing their own indicator has to do, and what they get without doing anything.
/// </summary>
public sealed class CustomIndicatorTests : GlobalTestData
{
    private const int SampleSize = 200;
    private const double Tolerance = 1e-10;

    private static StockData CreateData() => new(StockTestData.Take(SampleSize));

    /// <summary>
    /// A complete indicator. The calculation is the only thing written here - resolving the input,
    /// the moving average, the true range, publishing, chaining and branching all come from the base.
    /// </summary>
    [Indicator("Range Bands", Description = "A moving average with bands a multiple of ATR away.")]
    private sealed class RangeBands : IndicatorBase
    {
        public RangeBands(int length = 20, double multiplier = 2)
        {
            Length = length;
            Multiplier = multiplier;
        }

        public int Length { get; }

        public double Multiplier { get; }

        protected override void Calculate()
        {
            var basis = MovingAverage(MovingAvgType.SimpleMovingAverage, Length);
            var range = AverageTrueRange(Length);

            var upper = NewSeries();
            var lower = NewSeries();
            for (var i = 0; i < Count; i++)
            {
                upper.Add(basis[i] + (range[i] * Multiplier));
                lower.Add(basis[i] - (range[i] * Multiplier));
            }

            Publish("UpperBand", upper);
            Publish("MiddleBand", basis);
            Publish("LowerBand", lower);
            SetPrimary(basis);
        }
    }

    private sealed class NoPrimaryDeclared : IndicatorBase
    {
        protected override void Calculate() => Publish("OnlyOutput", MovingAverage(MovingAvgType.SimpleMovingAverage, 5));
    }

    private sealed class PublishesWrongLength : IndicatorBase
    {
        protected override void Calculate() => Publish("Short", new List<double> { 1, 2, 3 });
    }

    [Fact]
    public void CustomIndicator_PublishesItsOutputs()
    {
        var result = new RangeBands(20, 2).Run(CreateData());

        result.OutputValues.Should().ContainKeys("UpperBand", "MiddleBand", "LowerBand");
        result.OutputValues["UpperBand"].Should().HaveCount(SampleSize);
        result.CustomValuesList.Should().BeSameAs(result.OutputValues["MiddleBand"],
            "SetPrimary decides what a chained calculation continues from");
    }

    [Fact]
    public void CustomIndicator_ChainsBothWaysWithTheBuiltInsAndDoesNotMutateItsInput()
    {
        var data = CreateData();

        // Built-in first, then the custom one.
        var onAnIndicator = new RangeBands(10).Run(data.CalculateSimpleMovingAverage(50));
        onAnIndicator.OutputValues["MiddleBand"].Should().HaveCount(SampleSize);

        // Custom one first, then a built-in continuing from a named output.
        var bands = new RangeBands(20, 2).Run(data);
        var rsiOfUpper = bands.SeriesView("UpperBand").CalculateRelativeStrengthIndex(length: 14);
        rsiOfUpper.CustomValuesList.Should().HaveCount(SampleSize);
    }

    [Fact]
    public void CustomIndicator_DoesNotModifyTheDataItWasHanded()
    {
        // A fresh instance: the built-in Calculate* methods publish onto the object they are given, so
        // this has to be measured on data that nothing else has touched.
        var data = CreateData();

        new RangeBands(20, 2).Run(data);

        data.CustomValuesList.Should().BeEmpty("Run must not modify the data it was handed");
        data.OutputValues.Should().BeEmpty();
    }

    [Fact]
    public void CustomIndicator_WorksWithTheGeneratedAccessors()
    {
        var bands = new RangeBands(20, 2).Run(CreateData());

        // UpperBand() is generated from the built-in library, and works here because a custom
        // indicator publishes its outputs the same way and returns the same type.
        var viaAccessor = bands.UpperBand();

        viaAccessor.CustomValuesList.Should().BeSameAs(bands.OutputValues["UpperBand"]);
    }

    [Fact]
    public void CustomIndicator_BranchesFromEachOutputIndependently()
    {
        var bands = new RangeBands(20, 2).Run(CreateData());

        var upper = bands.SeriesView("UpperBand").CalculateRelativeStrengthIndex(length: 14).CustomValuesList;
        var lower = bands.SeriesView("LowerBand").CalculateRelativeStrengthIndex(length: 14).CustomValuesList;

        upper.Should().NotEqual(lower);
    }

    [Fact]
    public void CustomIndicator_TakesItsNameFromTheAttributeWhenOneIsGiven()
    {
        new RangeBands().Name.Should().Be("Range Bands");
        new NoPrimaryDeclared().Name.Should().Be("NoPrimaryDeclared", "the type name is the default");
    }

    [Fact]
    public void CustomIndicator_WithoutAnExplicitPrimary_ChainsFromItsFirstOutput()
    {
        var result = new NoPrimaryDeclared().Run(CreateData());

        result.CustomValuesList.Should().BeSameAs(result.OutputValues["OnlyOutput"]);
    }

    [Fact]
    public void CustomIndicator_PublishingASeriesThatDoesNotLineUpWithTheBarsIsRejected()
    {
        var act = () => new PublishesWrongLength().Run(CreateData());

        act.Should().Throw<CalculationException>()
            .WithMessage("*3 values for 200 bars*",
                "a series that does not line up with the price series would silently misalign every "
                + "reading taken from it");
    }

    /// <summary>
    /// The helpers take the series they measure as a parameter, so one cannot redefine another's input
    /// - the defect issue #145 describes is not expressible from inside a custom indicator.
    /// </summary>
    [Fact]
    public void CustomIndicator_ComputingAnAverageDoesNotChangeWhatTheDispersionReads()
    {
        var withAverageFirst = new OrderProbe(averageFirst: true).Run(CreateData());
        var withDispersionFirst = new OrderProbe(averageFirst: false).Run(CreateData());

        var a = withAverageFirst.OutputValues["StdDev"];
        var b = withDispersionFirst.OutputValues["StdDev"];

        for (var i = 0; i < a.Count; i++)
        {
            a[i].Should().BeApproximately(b[i], Tolerance, $"call order must not matter, index {i}");
        }
    }

    private sealed class OrderProbe : IndicatorBase
    {
        private readonly bool _averageFirst;

        public OrderProbe(bool averageFirst) => _averageFirst = averageFirst;

        protected override void Calculate()
        {
            if (_averageFirst)
            {
                MovingAverage(MovingAvgType.SimpleMovingAverage, 20);
                Publish("StdDev", StandardDeviation(20));
            }
            else
            {
                var dispersion = StandardDeviation(20);
                MovingAverage(MovingAvgType.SimpleMovingAverage, 20);
                Publish("StdDev", dispersion);
            }
        }
    }
}

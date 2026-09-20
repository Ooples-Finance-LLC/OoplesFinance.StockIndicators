using FluentAssertions;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

/// <summary>
/// The shape the generator has to reproduce for all 845 options types, held to by the hand-written specimens.
/// </summary>
/// <remarks>
/// Written before the generator so the generated output has something to be wrong against. Each assertion here
/// is a rule the emitter must follow, not an incidental property of these three.
/// </remarks>
public sealed class SpecimenIndicatorTests
{
    /// <summary>A caller's own average: implements the interface, has no MovingAvgType.</summary>
    private sealed class MyOwnAverage : IndicatorBase, IMovingAverage
    {
        public MyOwnAverage(int length) => Length = length;

        public int Length { get; }

        public override int WarmupBars => Length;

        protected internal override object CreateState() => new State();

        private sealed class State : IIndicatorState
        {
            public void Reset() { }

            public double Update(in Bar bar) => bar.Close;
        }
    }

    private sealed class NoArithmetic : IndicatorBase
    {
    }

    [Fact]
    public void ABuiltInNamesItsBatchIndicatorAndBuildsItsOptions()
    {
        var sma = new Sma(20);

        ((IBuiltInIndicator)sma).BatchName.Should().Be(IndicatorName.SimpleMovingAverage);
        ((IBuiltInIndicator)sma).BatchOutputKey.Should().BeNull();

        var options = ((IBuiltInIndicator)sma).CreateOptions();
        options.Should().BeOfType<SmaSpecOptions>();
        ((SmaSpecOptions)options).Length.Should().Be(20);
    }

    [Fact]
    public void BandsDefaultToASimpleAverageAndDeclareItAsAComponent()
    {
        var bands = new BollingerBands(20, 2);

        bands.Components.Should().ContainSingle();
        bands.Components[0].Should().BeOfType<Sma>();
        bands.Outputs.Should().HaveCount(3);
        bands.Upper.Slot.Should().Be(0);
        bands.Lower.Slot.Should().Be(2);
    }

    [Fact]
    public void OneOfOurAveragesCollapsesIntoTheEnumTheBatchCalculationTakes()
    {
        var bands = new BollingerBands(20, 2, new Ema(20));

        var options = (BollingerBandsSpecOptions)((IBuiltInIndicator)bands).CreateOptions();

        options.MaType.Should().Be(MovingAvgType.ExponentialMovingAverage,
            "a built-in average is something the existing batch calculation already knows how to run");
        options.Length.Should().Be(20);
        options.StdDevMult.Should().Be(2);
    }

    [Fact]
    public void ACallersOwnAverageStaysAComponentInsteadOfCollapsing()
    {
        var mine = new MyOwnAverage(20);
        var bands = new BollingerBands(20, 2, mine);

        var options = (BollingerBandsSpecOptions)((IBuiltInIndicator)bands).CreateOptions();

        // There is no enum member for someone else's average, which is exactly why the parameter is an
        // interface. It stays in the graph as a component and gets run.
        options.MaType.Should().Be(MovingAvgType.SimpleMovingAverage, "the default, not the caller's average");
        bands.Components.Should().ContainSingle().Which.Should().BeSameAs(mine);
    }

    [Fact]
    public void WarmupIsTheLongestOfTheIndicatorAndItsComponents()
    {
        new BollingerBands(20, 2).WarmupBars.Should().Be(20);
        new BollingerBands(20, 2, new Ema(50)).WarmupBars.Should().Be(50,
            "the bands cannot mean anything before their own middle line does");
    }

    [Fact]
    public void EveryIndicatorEitherIsItsOwnArithmeticOrNamesABatchIndicator()
    {
        FluentActions.Invoking(() => IndicatorContract.RequireComputable(new Sma(20)))
            .Should().NotThrow("a built-in names a batch indicator");

        FluentActions.Invoking(() => IndicatorContract.RequireComputable(new MyOwnAverage(20)))
            .Should().NotThrow("a custom indicator supplies a state");

        FluentActions.Invoking(() => IndicatorContract.RequireComputable(new NoArithmetic()))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*supplies no arithmetic*");
    }

    [Fact]
    public void AStateThatDoesNotMatchTheOutputCountIsRejected()
    {
        FluentActions.Invoking(() => IndicatorContract.RequireComputable(new WrongStateKind()))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*does not match the number of series it publishes*");
    }

    private sealed class WrongStateKind : MultiOutputIndicatorBase
    {
        public WrongStateKind() : base(2) => (First, Second) = DeclaredOutputs;

        public IIndicatorOutput First { get; }

        public IIndicatorOutput Second { get; }

        // Publishes two series but returns a single-output state.
        protected internal override object CreateState() => new State();

        private sealed class State : IIndicatorState
        {
            public void Reset() { }

            public double Update(in Bar bar) => bar.Close;
        }
    }
}

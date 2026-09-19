using FluentAssertions;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

/// <summary>
/// What the v2 indicator vocabulary guarantees before any engine runs it.
/// </summary>
/// <remarks>
/// <para>
/// These are the mistakes the shape is supposed to make impossible, so each one is asserted where it would be
/// made - in the constructor or the fluent call - rather than left to surface as a wrong series during a run.
/// Two of them replace defects the v1 custom-indicator API could only catch at calculation time: publishing
/// the wrong number of values, and failing to declare a primary output.
/// </para>
/// </remarks>
public sealed class IndicatorVocabularyTests
{
    private sealed class Single : Indicator
    {
        public Single(int warmup = 0) => WarmupOverride = warmup;

        public int WarmupOverride { get; }

        public override int WarmupBars => WarmupOverride;

        protected internal override object CreateState() => new State();

        private sealed class State : IIndicatorState
        {
            public void Reset() { }

            public double Update(in Bar bar) => bar.Close;
        }
    }

    private sealed class Bands : MultiOutputIndicator
    {
        public Bands() : base(3) => (Upper, Middle, Lower) = DeclaredOutputs;

        public IIndicatorOutput Upper { get; }

        public IIndicatorOutput Middle { get; }

        public IIndicatorOutput Lower { get; }

        protected internal override object CreateState() => new State();

        private sealed class State : IMultiOutputState
        {
            public void Reset() { }

            public void Update(in Bar bar, Span<double> outputs)
            {
                outputs[0] = bar.High;
                outputs[1] = bar.Close;
                outputs[2] = bar.Low;
            }
        }
    }

    private sealed class MiscountedBands : MultiOutputIndicator
    {
        // Declares three, assigns two. The mismatch is the point.
        public MiscountedBands() : base(3) => (First, Second) = DeclaredOutputs;

        public IIndicatorOutput First { get; }

        public IIndicatorOutput Second { get; }

        protected internal override object CreateState() => throw new NotSupportedException();
    }

    [Fact]
    public void ASingleOutputIndicatorIsItsOwnOutput()
    {
        var indicator = new Single();

        indicator.Outputs.Should().HaveCount(1,
            "run[indicator] has to mean something without the caller naming an output");
        indicator.Outputs[0].Indicator.Should().BeSameAs(indicator);
        indicator.Outputs[0].Slot.Should().Be(0);
        indicator.Value.Should().BeSameAs(indicator.Outputs[0]);
    }

    [Fact]
    public void EachDeclaredOutputKnowsItsIndicatorAndItsSlot()
    {
        var bands = new Bands();

        bands.Outputs.Should().HaveCount(3);
        bands.Upper.Slot.Should().Be(0);
        bands.Middle.Slot.Should().Be(1);
        bands.Lower.Slot.Should().Be(2);
        bands.Outputs.Should().OnlyContain(o => o.Indicator == bands);
    }

    [Fact]
    public void TwoInstancesOfTheSameIndicatorAreTwoIndicators()
    {
        var fast = new Single();
        var slow = new Single();

        // Identity is the object, so there is no name to collide and no last-one-wins.
        fast.Should().NotBeSameAs(slow);
        fast.Outputs[0].Should().NotBeSameAs(slow.Outputs[0]);
    }

    [Fact]
    public void DeclaringMoreOutputsThanAreAssignedIsCaughtInTheConstructor()
    {
        var act = () => new MiscountedBands();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*declared 3 outputs but is assigning 2*");
    }

    [Fact]
    public void AMultiOutputIndicatorPublishingOneSeriesIsRejected()
    {
        var act = () => new Bands().Outputs.Should().NotBeEmpty();

        act.Should().NotThrow("three is a valid count");
        FluentActions.Invoking(() => new OneOutput()).Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*derive from Indicator for one*");
    }

    private sealed class OneOutput : MultiOutputIndicator
    {
        public OneOutput() : base(1) { }

        protected internal override object CreateState() => throw new NotSupportedException();
    }

    [Fact]
    public void ChainingRecordsTheObjectRatherThanAName()
    {
        var source = new Single();
        var chained = new Single();

        chained.Of(source).Should().BeSameAs(chained, "Of is fluent");
        chained.Source.Should().BeSameAs(source);
        source.Source.Should().BeNull("nothing was chained onto the source");
    }

    [Fact]
    public void AnIndicatorCannotReadItself()
    {
        var indicator = new Single();

        var act = () => indicator.Of(indicator);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*computed from itself*");
    }

    [Fact]
    public void ALongerCycleIsCaughtWhereItIsCreated()
    {
        var first = new Single();
        var second = new Single().Of(first);
        var third = new Single().Of(second);

        // Closing the loop is rejected at the call that closes it, not as a cycle error during evaluation
        // naming handles the caller never saw.
        var act = () => first.Of(third);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*computed from itself*");
    }

    [Fact]
    public void ChainingOntoNothingIsRejected()
    {
        var act = () => new Single().Of(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WarmupIsWhateverTheIndicatorSays()
    {
        new Single().WarmupBars.Should().Be(0);
        new Single(warmup: 34).WarmupBars.Should().Be(34);
    }
}

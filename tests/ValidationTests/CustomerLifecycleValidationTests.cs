using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class CustomerLifecycleValidationTests
{
    // These specimens test cumulative-state reset, with bounded prices and an int-sized history.
    // Their raw summation is not a general finite-double numerical implementation.
    private static readonly IndicatorInputDomain LifecycleDomain = IndicatorInputDomain.Finite
        .WithRange(IndicatorInputFields.Close, -1e6, 1e6);

    [Fact]
    public async Task NestedCustomerSourceCannotHideItsBrokenResetBehindABuiltIn()
    {
        IIndicator Create() => new Sma(2).Of(new Scalar(broken: true));
        var report = await IndicatorValidation.ValidateAsync(new(Create().GetType(), "nested-reset", Create),
            IndicatorValidationOptions.Smoke());
        Assert.Contains(report.Failures, failure => failure.Rule == "Reset" && failure.Message.Contains("after reset"));
        Assert.Throws<IndicatorValidationException>(report.ThrowIfInvalid);
    }

    [Theory]
    [InlineData(0, false)] [InlineData(1, false)] [InlineData(2, false)] [InlineData(3, false)]
    [InlineData(0, true)] [InlineData(1, true)] [InlineData(2, true)] [InlineData(3, true)]
    public async Task EveryCustomerStateInterfaceGetsAutomaticResetValidation(int shape, bool broken)
    {
        IIndicator Create() => shape switch
        {
            0 => new Scalar(broken), 1 => new Multiple(broken),
            2 => new Composed(broken), _ => new ComposedMultiple(broken)
        };
        var report = await IndicatorValidation.ValidateAsync(new(Create().GetType(), "reset", Create));
        if (broken)
        {
            Assert.Contains(report.Failures, failure => failure.Rule == "Reset" && failure.Message.Contains("after reset"));
            Assert.Contains(report.FixtureEvidence, fixture => fixture.InputBars > 0 && !fixture.CustomerResetChecked && !fixture.Passed);
            Assert.Throws<IndicatorValidationException>(report.ThrowIfInvalid);
        }
        else
        {
            report.ThrowIfInvalid();
            Assert.All(report.FixtureEvidence, fixture => Assert.True(fixture.CustomerResetChecked));
        }
    }

    private static IEnumerable<IndicatorValidationRule> Rules(bool composed, int slots)
    {
        for (var slot = 0; slot < slots; slot++)
        {
            var sign = slot == 0 ? 1 : -1;
            yield return IndicatorValidationRule.Reference(slot, bars => bars.Select((_, i) => sign *
                Enumerable.Range(0, i + 1).Sum(j => bars[j].Close + (composed
                    ? bars[j].Close + (j == 0 ? 0 : (bars[j - 1].Close + bars[j].Close) / 2) : 0))).ToArray());
        }
    }

    public sealed class Scalar(bool broken = false) : IndicatorBase, IIndicatorValidationContract, IIndicatorInputDomainContract
    {
        public IndicatorInputDomain InputDomain => LifecycleDomain;
        public IEnumerable<IndicatorValidationRule> ValidationRules => Rules(false, 1);
        protected internal override object CreateState() => new SingleState(broken);
    }

    public sealed class Multiple(bool broken = false) : MultiOutputIndicatorBase(2), IIndicatorValidationContract, IIndicatorInputDomainContract
    {
        public IndicatorInputDomain InputDomain => LifecycleDomain;
        public IEnumerable<IndicatorValidationRule> ValidationRules => Rules(false, 2);
        protected internal override object CreateState() => new MultiState(broken);
    }

    public sealed class Composed : IndicatorBase, IIndicatorValidationContract, IIndicatorInputDomainContract
    {
        public IndicatorInputDomain InputDomain => LifecycleDomain;
        private readonly bool _broken;
        public Composed(bool broken = false)
        { _broken = broken; Uses(new Sma(2), new SharedIndicatorValidationTests.Identity()); }
        public IEnumerable<IndicatorValidationRule> ValidationRules => Rules(true, 1);
        protected internal override object CreateState() => new ComposedState(_broken);
    }

    public sealed class ComposedMultiple : MultiOutputIndicatorBase, IIndicatorValidationContract, IIndicatorInputDomainContract
    {
        public IndicatorInputDomain InputDomain => LifecycleDomain;
        private readonly bool _broken;
        public ComposedMultiple(bool broken = false) : base(2)
        { _broken = broken; Uses(new Sma(2), new SharedIndicatorValidationTests.Identity()); }
        public IEnumerable<IndicatorValidationRule> ValidationRules => Rules(true, 2);
        protected internal override object CreateState() => new ComposedMultiState(_broken);
    }

    private abstract class Accumulator(bool broken)
    {
        private double _sum;
        public void Reset() { if (!broken) _sum = 0; }
        protected double Add(in Bar bar, ReadOnlySpan<double> components = default)
        {
            _sum += bar.Close;
            foreach (var value in components) _sum += value;
            return _sum;
        }
    }
    private sealed class SingleState(bool broken) : Accumulator(broken), IIndicatorState
    { public double Update(in Bar bar) => Add(in bar); }
    private sealed class MultiState(bool broken) : Accumulator(broken), IMultiOutputState
    { public void Update(in Bar bar, Span<double> outputs) { outputs[0] = Add(in bar); outputs[1] = -outputs[0]; } }
    private sealed class ComposedState(bool broken) : Accumulator(broken), IComposedIndicatorState
    { public double Update(in Bar bar, ReadOnlySpan<double> components) => Add(in bar, components); }
    private sealed class ComposedMultiState(bool broken) : Accumulator(broken), IComposedMultiOutputState
    { public void Update(in Bar bar, ReadOnlySpan<double> components, Span<double> outputs) { outputs[0] = Add(in bar, components); outputs[1] = -outputs[0]; } }
}

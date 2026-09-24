using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed partial class SharedIndicatorValidationTests
{
    private static IndicatorValidationCase Case<T>(Func<IIndicator> factory,
        params IndicatorValidationRule[] rules) where T : IIndicator => new(typeof(T), "test", factory, rules);

    [Fact]
    public async Task ACustomerIndicatorGetsEveryFixtureAndItsReferenceContract()
    {
        var report = await IndicatorValidation.ValidateAsync(Case<Identity>(() => new Identity()));
        report.ThrowIfInvalid();
        var required = new[] { "empty", "single", "flat", "zero-price", "negative-price", "rising", "falling",
            "alternating", "spike", "zero-volume", "walk-31", "walk-42", "sessions" }
            .Concat(IndicatorAdversarialCases.Generate(256, 244).Select(f => f.Name)).ToArray();
        Assert.Equal(required, report.FixtureEvidence.Select(f => f.Name));
        Assert.Equal(required.Length, report.FixturesCompleted);
        Assert.Equal(report.ValuesChecked, report.FixtureEvidence.Sum(f => f.ValuesChecked));
        Assert.All(report.FixtureEvidence, f => { Assert.True(f.Completed); Assert.True(f.Passed); Assert.Equal(f.InputBars, f.ValuesChecked); });
    }

    [Fact]
    public async Task CustomerDiscoveryAutomaticallyDetectsErasedTinyInputs()
    {
        var testCase = Assert.Single(IndicatorValidationDiscovery.Discover(new[] { typeof(ErasesTinyInputs).Assembly })
            .Where(c => c.IndicatorType == typeof(ErasesTinyInputs)));
        var report = await IndicatorValidation.ValidateAsync(testCase);
        Assert.Contains(report.Failures, f => f.Fixture.EndsWith("/tiny") && f.Rule == "Reference[0]");
        Assert.Contains(report.Failures, f => f.Fixture.EndsWith("/subnormal") && f.Rule == "Reference[0]");
        Assert.Throws<IndicatorValidationException>(report.ThrowIfInvalid);
    }

    [Fact]
    public async Task NonfiniteSecondaryOutputsAreFailures()
    {
        var report = await IndicatorValidation.ValidateAsync(Case<BrokenSecondary>(() => new BrokenSecondary()));
        Assert.Contains(report.Failures, f => f.Rule == "Finite" && f.Message.Contains("Output 1"));
        Assert.Throws<IndicatorValidationException>(report.ThrowIfInvalid);
    }

    [Fact]
    public async Task ExceptionsCannotBecomeSkips()
    {
        var report = await IndicatorValidation.ValidateAsync(Case<ThrowsOnUpdate>(() => new ThrowsOnUpdate()));
        Assert.Contains(report.Failures, f => f.Rule == "Execution" && f.Message.Contains("deliberate failure"));
        Assert.False(report.IsValid);
        Assert.Contains(report.FixtureEvidence, f => f.InputBars > 0 && !f.Completed && !f.Passed);
    }

    [Fact]
    public async Task AFiniteButIncorrectFormulaFailsItsReference()
    {
        var report = await IndicatorValidation.ValidateAsync(Case<WrongFormula>(() => new WrongFormula()));
        Assert.Contains(report.Failures, f => f.Rule == "Reference[0]");
        Assert.Contains(report.FixtureEvidence, f => f.Completed && !f.Passed);
    }

    [Fact]
    public async Task DefaultCustomerReferenceDetectsFiniteStartupFormulaErrors()
    {
        var report = await IndicatorValidation.ValidateAsync(Case<WrongStartup>(() => new WrongStartup()));
        Assert.Contains(report.Failures, f => f.Fixture == "single" && f.Rule == "Reference[0]"
            && f.Message.Contains("bar 0"));
        Assert.Contains(report.Failures, f => f.Fixture == "before-warmup" && f.Rule == "Reference[0]");
        Assert.Throws<IndicatorValidationException>(report.ThrowIfInvalid);
    }

    [Fact]
    public async Task StrictValidationRejectsExplicitlyMissingStartupFormulaEvidence()
    {
        var testCase = Case<StartupExcluded>(() => new StartupExcluded());
        var report = await IndicatorValidation.ValidateAsync(testCase);
        Assert.Contains(report.Failures, f => f.Rule == "StartupFormulaReference");
        Assert.Equal(new[] { 0 }, report.FormulaCoverage!.MissingStartupOutputSlots);
        Assert.False(report.FormulaCoverage.HasCompleteStartupReferences);
        Assert.False(report.FormulaCoverage.HasCompleteIndependentTrajectories);
        Assert.Throws<IndicatorValidationException>(report.ThrowIfInvalid);
        (await IndicatorValidation.ValidateAsync(testCase, IndicatorValidationOptions.Smoke())).ThrowIfInvalid();
    }

    [Fact]
    public async Task StrictValidationRejectsRecurrenceOnlyEvidenceEvenWhenEveryResidualPasses()
    {
        var testCase = Case<RecurrenceOnly>(() => new RecurrenceOnly());
        var report = await IndicatorValidation.ValidateAsync(testCase);
        Assert.True(report.FormulaCoverage!.IsComplete);
        Assert.False(report.FormulaCoverage.HasCompleteIndependentTrajectories);
        Assert.Equal(new[] { 0 }, report.FormulaCoverage.RecurrenceOnlyOutputSlots);
        Assert.Contains(report.Failures, f => f.Rule == "IndependentFormulaTrajectory");
        Assert.Throws<IndicatorValidationException>(report.ThrowIfInvalid);
        (await IndicatorValidation.ValidateAsync(testCase, IndicatorValidationOptions.Smoke())).ThrowIfInvalid();
    }

    [Fact]
    public async Task StartupResidualCannotFillAnIndependentTrajectoryStartupGap()
    {
        var report = await IndicatorValidation.ValidateAsync(Case<ResidualStartup>(() => new ResidualStartup()));
        Assert.True(report.FormulaCoverage!.IsComplete);
        Assert.Empty(report.FormulaCoverage.RecurrenceOnlyOutputSlots);
        Assert.Equal(new[] { 0 }, report.FormulaCoverage.MissingStartupOutputSlots);
        Assert.False(report.FormulaCoverage.HasCompleteIndependentTrajectories);
        Assert.Contains(report.Failures, f => f.Rule == "StartupFormulaReference");
        Assert.Throws<IndicatorValidationException>(report.ThrowIfInvalid);
    }

    [Fact]
    public void ReferenceUnavailableStartupRequiresExactStatusAgreement()
    {
        var bars = new[] { new Bar(DateTime.UnixEpoch, 1, 1, 1, 1, 1) };
        var unavailable = IndicatorValidationRule.Reference(0, _ => new[] { double.NaN });
        unavailable.Check(new("startup", bars, new[] { new[] { double.NaN } }, 1));
        Assert.Throws<InvalidOperationException>(() => unavailable.Check(new("wrong-status", bars, new[] { new[] { 0d } }, 1)));
        Assert.Throws<InvalidOperationException>(() => unavailable.Check(new("late-unavailable", bars, new[] { new[] { double.NaN } }, 0)));
        var finite = IndicatorValidationRule.Reference(0, _ => new[] { 0d });
        Assert.Throws<InvalidOperationException>(() => finite.Check(new("missing-value", bars, new[] { new[] { double.NaN } }, 1)));
    }

    [Fact]
    public async Task CustomerFixtureNamesCannotMaskRequiredInputClasses()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => IndicatorValidation.ValidateAsync(Case<Identity>(() => new Identity()),
            new() { AdditionalFixtures = new[] { new IndicatorValidationFixture("zero-price", Array.Empty<Bar>()) } }));
    }

    [Fact]
    public async Task NondeterminismIsDetectedAcrossFreshInstances()
    {
        var report = await IndicatorValidation.ValidateAsync(Case<Unstable>(() => new Unstable()));
        Assert.Contains(report.Failures, f => f.Rule == "Determinism");
    }

    [Fact]
    public async Task AFailureThatOnlyOccursOnZeroVolumeIsDetected()
    {
        var report = await IndicatorValidation.ValidateAsync(Case<ZeroVolumeBug>(() => new ZeroVolumeBug()));
        Assert.Contains(report.Failures, f => f.Fixture == "zero-volume" && f.Rule == "Finite");
    }

    [Fact]
    public async Task BoundsAreOptInAndViolationsThrow()
    {
        var testCase = Case<Identity>(() => new Identity(), IndicatorValidationRule.Bounds(0, 0, 90));
        var exception = await Assert.ThrowsAsync<IndicatorValidationException>(
            () => IndicatorValidation.ValidateAndThrowAsync(testCase));
        Assert.Contains(exception.Report.Failures, f => f.Rule == "Bounds[0]");
    }

    [Fact]
    public async Task ExcessiveWarmupCannotMakeValidationVacuous()
    {
        var report = await IndicatorValidation.ValidateAsync(Case<ExcessiveWarmup>(() => new ExcessiveWarmup()));
        Assert.False(report.IsValid);
        Assert.Contains(report.Failures, f => f.Message.Contains("warmup"));
    }

    [Fact]
    public async Task ACustomersMovingAverageAutomaticallyGetsAConstantPreservationCheck()
    {
        var report = await IndicatorValidation.ValidateAsync(Case<BadAverage>(() => new BadAverage()));
        Assert.True(report.MathematicalRuleCount > 0);
        Assert.Contains(report.Failures, f => f.Rule == "ConstantAverage" && f.Fixture == "settled-flat-50");
    }

    [Fact]
    public async Task StrictMathematicalCoverageCannotPassWithOnlyUniversalChecks()
    {
        var report = await IndicatorValidation.ValidateAsync(Case<ZeroVolumeBug>(() => new ZeroVolumeBug()),
            new IndicatorValidationOptions { RequireMathematicalContract = true });
        Assert.Equal(0, report.MathematicalRuleCount);
        Assert.Contains(report.Failures, f => f.Rule == "MathematicalContract");
    }

    [Fact]
    public async Task ReusingAnInstanceIsAnExplicitFactoryError()
    {
        var reused = new Identity();
        var report = await IndicatorValidation.ValidateAsync(Case<Identity>(() => reused));
        Assert.Contains(report.Failures, f => f.Message.Contains("fresh instance"));
    }

    [Fact]
    public async Task DiscoveryIncludesCustomerAssemblyAndFailsUnsupportedConstructors()
    {
        var cases = IndicatorValidationDiscovery.Discover(new[] { typeof(Identity).Assembly });
        Assert.Contains(cases, c => c.IndicatorType == typeof(Identity));
        var missing = Assert.Single(cases, c => c.IndicatorType == typeof(NeedsDependency));
        var report = await IndicatorValidation.ValidateAsync(missing);
        Assert.Contains(report.Failures, f => f.Rule == "Discovery" && f.Message.Contains("Register a factory"));

        var registration = Case<NeedsDependency>(() => new NeedsDependency(new object()));
        var registered = IndicatorValidationDiscovery.Discover(new[] { typeof(Identity).Assembly }, new[] { registration });
        Assert.Same(registration, Assert.Single(registered, c => c.IndicatorType == typeof(NeedsDependency)));
        await IndicatorValidation.ValidateAndThrowAsync(registration);
    }

    [Fact]
    public void DiscoveryCoversEveryBuiltInTypeIncludingRequiredPeriods()
    {
        var assembly = typeof(IIndicator).Assembly;
        var cases = IndicatorValidationDiscovery.Discover(new[] { assembly });
        var expected = assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract && typeof(IIndicator).IsAssignableFrom(t));
        cases.Select(c => c.IndicatorType).Distinct().Should().BeEquivalentTo(expected);
        var stdDev = cases.Where(c => c.IndicatorType == typeof(StdDev)).ToArray();
        Assert.Equal(4, stdDev.Length);
        Assert.Equal(4, stdDev.Select(c => ((StdDev)c.Factory()).Length).Distinct().Count());
        Assert.All(cases, c => Assert.NotEqual("discovery", c.Name));
        var awesomeWeighted = Assert.Single(cases, c => c.IndicatorType == typeof(AwesomeOscillator) && c.Name == "weighted-average");
        Assert.Single(awesomeWeighted.Factory().Components);
    }

    [Fact]
    public async Task CancellationPropagatesInsteadOfBeingRecordedAsAnIndicatorFailure()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => IndicatorValidation.ValidateAsync(
            Case<Identity>(() => new Identity()), cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task CustomerDiscoveryExercisesMinimumPeriodsAndThrowsForAWrongFormula()
    {
        var cases = IndicatorValidationDiscovery.Discover(new[] { typeof(CustomerWithPeriods).Assembly });
        var minimum = Assert.Single(cases, c => c.IndicatorType == typeof(CustomerWithPeriods) && c.Name == "minimum-periods");
        var indicator = (CustomerWithPeriods)minimum.Factory();
        Assert.Equal(1, indicator.Length);
        Assert.Equal(1, indicator.SignalPeriod);
        await IndicatorValidation.ValidateAndThrowAsync(minimum, new() { RequireFormulaReference = true });
        var broken = Assert.Single(cases, c => c.IndicatorType == typeof(BrokenMinimumPeriod) && c.Name == "minimum-periods");
        await Assert.ThrowsAsync<IndicatorValidationException>(() => IndicatorValidation.ValidateAndThrowAsync(broken,
            new() { RequireFormulaReference = true }));
    }

    public sealed class CustomerWithPeriods : Identity
    {
        public CustomerWithPeriods(int length = 14, int signalPeriod = 5)
        { Length = length; SignalPeriod = signalPeriod; }
        public int Length { get; }
        public int SignalPeriod { get; }
    }

    public sealed class BrokenMinimumPeriod : Identity
    {
        private readonly int _length;
        public BrokenMinimumPeriod(int length = 14) => _length = length;
        protected internal override object CreateState() => new State(_length);
        private sealed class State(int length) : IIndicatorState
        {
            public void Reset() { }
            public double Update(in Bar bar) => length == 1 ? bar.Close + 1 : bar.Close;
        }
    }

    public class Identity : IndicatorBase, IIndicatorValidationContract
    {
        public IEnumerable<IndicatorValidationRule> ValidationRules => new[]
        {
            IndicatorValidationRule.Reference(0, bars => bars.Select(b => b.Close).ToArray(), 0, 0)
        };
        protected internal override object CreateState() => new IdentityState();
        private sealed class IdentityState : IIndicatorState
        {
            public void Reset() { }
            public double Update(in Bar bar) => bar.Close;
        }
    }

    public sealed class ErasesTinyInputs : Identity
    {
        protected internal override object CreateState() => new State();
        private sealed class State : IIndicatorState
        {
            public void Reset() { }
            public double Update(in Bar bar) => Math.Abs(bar.Close) < 1e-50 ? 0 : bar.Close;
        }
    }

    public sealed class WrongFormula : Identity
    {
        protected internal override object CreateState() => new State();
        private sealed class State : IIndicatorState
        {
            public void Reset() { }
            public double Update(in Bar bar) => bar.Close + 1;
        }
    }

    public sealed class WrongStartup : Identity
    {
        public override int WarmupBars => 5;
        protected internal override object CreateState() => new State();
        private sealed class State : IIndicatorState
        {
            private int _count;
            public void Reset() => _count = 0;
            public double Update(in Bar bar) => ++_count <= 5 ? bar.Close + 1 : bar.Close;
        }
    }

    public sealed class RecurrenceOnly : Identity, IIndicatorValidationContract
    {
        IEnumerable<IndicatorValidationRule> IIndicatorValidationContract.ValidationRules => new[]
        {
            IndicatorValidationRule.ReferenceRecurrence(0, context => context.Bars.Select(b => b.Close).ToArray())
        };
    }

    public sealed class ResidualStartup : Identity, IIndicatorValidationContract
    {
        public override int WarmupBars => 5;
        IEnumerable<IndicatorValidationRule> IIndicatorValidationContract.ValidationRules => new[]
        {
            IndicatorValidationRule.ReferenceRecurrence(0, context => context.Bars.Select(b => b.Close).ToArray()),
            IndicatorValidationRule.Reference(0, bars => bars.Select(b => b.Close).ToArray(),
                IndicatorErrorBudget.Exact, includeWarmup: false)
        };
    }

    public sealed class StartupExcluded : Identity, IIndicatorValidationContract
    {
        public override int WarmupBars => 5;
        IEnumerable<IndicatorValidationRule> IIndicatorValidationContract.ValidationRules => new[]
        {
            IndicatorValidationRule.Reference(0, bars => bars.Select(b => b.Close).ToArray(),
                new IndicatorErrorBudget(0, 0), includeWarmup: false)
        };
    }

    public sealed class BrokenSecondary : MultiOutputIndicatorBase
    {
        public BrokenSecondary() : base(2) { }
        protected internal override object CreateState() => new State();
        private sealed class State : IMultiOutputState
        {
            public void Reset() { }
            public void Update(in Bar bar, Span<double> outputs) { outputs[0] = bar.Close; outputs[1] = double.NaN; }
        }
    }

    public sealed class ThrowsOnUpdate : IndicatorBase
    {
        protected internal override object CreateState() => new State();
        private sealed class State : IIndicatorState
        {
            public void Reset() { }
            public double Update(in Bar bar) => throw new InvalidOperationException("deliberate failure");
        }
    }

    public sealed class Unstable : IndicatorBase
    {
        private static int _sequence;
        protected internal override object CreateState() => new State();
        private sealed class State : IIndicatorState
        {
            public void Reset() { }
            public double Update(in Bar bar) => Interlocked.Increment(ref _sequence);
        }
    }

    public sealed class ZeroVolumeBug : IndicatorBase
    {
        protected internal override object CreateState() => new State();
        private sealed class State : IIndicatorState
        {
            public void Reset() { }
            public double Update(in Bar bar) => bar.Volume == 0 ? double.NaN : bar.Close;
        }
    }

    public sealed class ExcessiveWarmup : Identity
    {
        public override int WarmupBars => int.MaxValue;
    }

    public sealed class NeedsDependency : Identity
    {
        public NeedsDependency(object dependency) { }
    }

    public sealed class BadAverage : IndicatorBase, IMovingAverage
    {
        public int Length => 5;
        protected internal override object CreateState() => new State();
        private sealed class State : IIndicatorState
        {
            public void Reset() { }
            public double Update(in Bar bar) => 100;
        }
    }
}

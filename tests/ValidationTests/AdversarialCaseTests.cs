using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class AdversarialCaseTests
{
    [Fact]
    public void TwoPointCorrelationCannotAccumulateRoundingResidues()
    {
        Assert.Equal(1, OoplesFinance.StockIndicators.Helpers.WindowCorrelation.Pearson(
            new[] { 749d, 750 }, new[] { 1e12, 1e12 + .125 }));
        Assert.Equal(-1, OoplesFinance.StockIndicators.Helpers.WindowCorrelation.Pearson(
            new[] { 749d, 750 }, new[] { 1e12 + .125, 1e12 }));
        Assert.Equal(0, OoplesFinance.StockIndicators.Helpers.WindowCorrelation.Pearson(
            new[] { 749d, 750 }, new[] { 1e12, 1e12 }));
    }

    [Fact]
    public async Task OneBarSchaffCycleHasZeroRangeInBothPasses()
    {
        var bars = Enumerable.Range(0, 70).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 100 + i, 100 + i, 100 + i, 100 + i, 1)).ToArray();
        var indicator = new SchaffTrendCycleShk(cycleLength: 1);
        using var run = await new OoplesFinance.StockIndicators.Builder.StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        Assert.All(run[indicator.Outputs[0]].ToArray(), value => Assert.Equal(0, value));
    }

    [Fact]
    public async Task ForceIndexDoesNotNormalizeAnExactZeroGapIntoAMaximumSignal()
    {
        // With alpha=1/3, two cascaded EMAs are exactly equal two bars after an isolated spike.
        var bars = Enumerable.Repeat(100d, 128).Concat(new[] { 200d, 100, 100 })
            .Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var indicator = new TrendDirectionForceIndex(length1: 10, length2: 1);
        using var run = await new OoplesFinance.StockIndicators.Builder.StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        var output = run[indicator.Outputs[0]].ToArray();
        Assert.Equal(1, output[128]);
        Assert.Equal(-1, output[129]);
        Assert.Equal(0, output.Last());
    }
    [Fact]
    public async Task ReversedDamaPeriodsApplyTheFinalSlowPeriodCap()
    {
        var bars = new[] { 3d, 1, 8, 2 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var indicator = new DynamicallyAdjustableMovingAverage(6, 1);
        using var run = await new OoplesFinance.StockIndicators.Builder.StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(new[] { 3d, 1, 8, 2 }, run[indicator].ToArray());
        Assert.Single(BuiltInFormulaReferences.For(indicator)).Check(new IndicatorValidationContext("slow-cap", bars,
            new[] { (IReadOnlyList<double>)new[] { 3d, 1, 8, 2 } }, 0));
    }
    [Fact]
    public void GeneratorReplaysSeedsAndPreservesExtremeInputClasses()
    {
        var first = IndicatorAdversarialCases.Generate(32, 244);
        var replay = IndicatorAdversarialCases.Generate(32, 244);
        Assert.Equal(18, first.Count);
        Assert.All(first.Zip(replay), pair => Assert.Equal(pair.First.Bars, pair.Second.Bars));
        var tiny = first.Single(f => f.Name.EndsWith("/tiny"));
        Assert.All(tiny.Bars, bar => Assert.InRange(bar.Close, 1e-100, 2e-100));
        var subnormal = first.Single(f => f.Name.EndsWith("/subnormal"));
        Assert.All(subnormal.Bars, bar => Assert.InRange(bar.Close, double.Epsilon, 16 * double.Epsilon));
        Assert.All(first.Single(f => f.Name.EndsWith("/overflow-adjacent")).Bars,
            bar => Assert.InRange(bar.Close, double.MaxValue / 2, double.MaxValue));
        Assert.Equal(new[] { 1e100, 1d, -1e100 }, first.Single(f => f.Name.EndsWith("/cancelled-spike")).Bars.Take(3).Select(bar => bar.Close));
        Assert.NotEqual(tiny.Bars[0].Close, IndicatorAdversarialCases.Generate(32, 245)[0].Bars[0].Close);
        Assert.All(first.SelectMany(f => f.Bars), bar =>
        {
            Assert.All(new[] { bar.Open, bar.High, bar.Low, bar.Close, bar.Volume }, value => Assert.True(double.IsFinite(value)));
            Assert.True(bar.Low <= Math.Min(bar.Open, bar.Close));
            Assert.True(bar.High >= Math.Max(bar.Open, bar.Close));
        });
        Assert.All(first.Single(f => f.Name.EndsWith("/volume-overflow-adjacent")).Bars,
            bar => Assert.InRange(bar.Volume, double.MaxValue / 2, double.MaxValue));
        Assert.All(first.Single(f => f.Name.EndsWith("/volume-subnormal")).Bars,
            bar => Assert.InRange(bar.Volume, double.Epsilon, 16 * double.Epsilon));
        Assert.Contains(first.Single(f => f.Name.EndsWith("/mixed-ohlc-extremes")).Bars,
            bar => bar.Open == double.Epsilon && bar.Close == 2 * double.Epsilon && bar.High == -bar.Low);
        Assert.All(first.Single(f => f.Name.EndsWith("/mixed-ohlc-subnormal")).Bars,
            bar => Assert.True(bar.Low < 0 && bar.High > 0 && bar.Open != bar.Close));
    }

    [Fact]
    public void ShrinkingPreservesOrderedTriggerAndReportsExhaustionHonestly()
    {
        var bars = Enumerable.Range(0, 40).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), i, i, i, i, 1)).ToArray();
        bool Fails(IReadOnlyList<Bar> input) => input.Any(b => b.Close == 7) && input.Any(b => b.Close == 31)
            && input.Zip(input.Skip(1), (a, b) => a.Time < b.Time).All(v => v);
        var fixture = new IndicatorValidationFixture("two-event-defect", bars);
        var result = IndicatorAdversarialCases.Shrink(fixture, Fails);
        Assert.True(result.IsComplete);
        Assert.Equal(new[] { 7d, 31d }, result.Fixture.Bars.Select(b => b.Close));
        var limited = IndicatorAdversarialCases.Shrink(fixture, Fails, maximumAttempts: 1);
        Assert.False(limited.IsComplete);
        Assert.Equal(bars, limited.Fixture.Bars);
        Assert.Throws<ArgumentException>(() => IndicatorAdversarialCases.Shrink(fixture, _ => false));
    }

    [Fact]
    public async Task CustomerContractsReceiveReplayInputsAndFailuresRetainTheirNames()
    {
        var fixture = IndicatorAdversarialCases.Generate(16, 245).Single(f => f.Name.EndsWith("/tiny"));
        var report = await IndicatorValidation.ValidateAsync(new(typeof(SharedIndicatorValidationTests.Identity), "tiny",
            () => new SharedIndicatorValidationTests.Identity()), new() { AdditionalFixtures = new[] { fixture } });
        report.ThrowIfInvalid();
        Assert.Contains(report.FixtureEvidence, f => f.Name == "xorshift32-v1/seed-244/tiny" && f.Passed);
        Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Passed);
        var broken = await IndicatorValidation.ValidateAsync(new(typeof(SharedIndicatorValidationTests.WrongFormula), "tiny",
            () => new SharedIndicatorValidationTests.WrongFormula()), new() { AdditionalFixtures = new[] { fixture } });
        Assert.Contains(broken.Failures, f => f.Fixture == fixture.Name && f.Rule == "Reference[0]");
    }

    [Fact]
    public void DiscoveryVariesPeriodsIndependently()
    {
        var cases = IndicatorValidationDiscovery.Discover(new[] { typeof(SharedIndicatorValidationTests.CustomerWithPeriods).Assembly });
        var testCase = Assert.Single(cases, c => c.IndicatorType == typeof(SharedIndicatorValidationTests.CustomerWithPeriods)
            && c.Name == "minimum-signalPeriod");
        var indicator = (SharedIndicatorValidationTests.CustomerWithPeriods)testCase.Factory();
        Assert.Equal(14, indicator.Length);
        Assert.Equal(1, indicator.SignalPeriod);
    }

    [Fact]
    public async Task CustomerValidationAutomaticallyDetectsErasedTinyVolume()
    {
        var report = await IndicatorValidation.ValidateAsync(new(typeof(ErasedTinyVolume), "volume-input",
            () => new ErasedTinyVolume()));
        Assert.Contains(report.Failures, failure => failure.Fixture.EndsWith("/volume-tiny") && failure.Rule == "Reference[0]");
        Assert.Contains(report.Failures, failure => failure.Fixture.EndsWith("/volume-subnormal") && failure.Rule == "Reference[0]");
        Assert.Throws<IndicatorValidationException>(report.ThrowIfInvalid);
    }

    private sealed class ErasedTinyVolume : IndicatorBase, IIndicatorValidationContract
    {
        public IEnumerable<IndicatorValidationRule> ValidationRules => new[]
        {
            IndicatorValidationRule.Reference(0, bars => bars.Select(bar => bar.Volume).ToArray(), IndicatorErrorBudget.Exact)
        };
        protected internal override object CreateState() => new State();
        private sealed class State : IIndicatorState
        {
            public void Reset() { }
            public double Update(in Bar bar) => bar.Volume < 1e-50 ? 0 : bar.Volume;
        }
    }
}

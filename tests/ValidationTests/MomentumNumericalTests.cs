using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class MomentumNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(MomentumOscillator) || c.IndicatorType == typeof(Momentum)).Select(c => new object[] { c });

    [Fact]
    public void RatioRoundsOnlyAfterScalingAndKeepsZeroDenominatorPolicy()
    {
        Assert.Equal(100, Helpers.RoundedMomentumRatio.Of(double.MaxValue, double.MaxValue));
        Assert.Equal(-100, Helpers.RoundedMomentumRatio.Of(-double.MaxValue, double.MaxValue));
        Assert.Equal(double.Epsilon, Helpers.RoundedMomentumRatio.Of(double.Epsilon, 100));
        Assert.Equal(0, Helpers.RoundedMomentumRatio.Of(12, 0));
        Assert.Equal(double.PositiveInfinity, Helpers.RoundedMomentumRatio.Of(double.MaxValue, double.Epsilon));
        Assert.Equal(double.NegativeInfinity, Helpers.RoundedMomentumRatio.Of(-double.MaxValue, double.Epsilon));
    }

    [Fact]
    public void DiscoveryIncludesEveryPromotedComposition()
        => Assert.Equal(54, Cases.Count(row => ((IndicatorValidationCase)row[0]).Name.StartsWith("momentum-composition/")));

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var sign in new[] { "positive", "negative" })
            Assert.Equal(2, Assert.Single(report.FixtureEvidence, f => f.Name == "momentum-" + sign + "-output-overflow").OutputOverflowRejectionsChecked);
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(14)]
    public async Task AllStagesMatchTheIndependentReferenceAcrossRoutes(int length)
    {
        var averages = new (IMovingAverage Average, int Kind)[] {
            (new Sma(),1), (new Wma(),2), (new Ema(),3), (new Wwma(),6),
            (new SymmetricallyWeightedMovingAverage(),7), (new FibonacciWeightedMovingAverage(),8),
            (new SquareRootWeightedMovingAverage(),9), (new ParabolicWma(),10), (new CubedWeightedMovingAverage(),11),
            (new QuickMovingAverage(),12), (new JsaMovingAverage(),13), (new QuadraticMovingAverage(),14),
            (new Kama(),15), (new SineWma(),16), (new NaturalMa(),17), (new EhlersHannMovingAverage(),18), (new Vidya(),19) };
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        foreach (var (average, kind) in averages)
        {
            var bars = fixture.Bars;
            var raw = BuiltInFormulaReferences.RoundedMomentum(bars, length);
            var count = Array.FindIndex(raw, double.IsInfinity);
            if (count >= 0) bars = bars.Take(count).ToArray();
            var expected = new Dictionary<string, double[]> {
                ["Mo"] = BuiltInFormulaReferences.RoundedMomentum(bars, length),
                ["Signal"] = BuiltInFormulaReferences.RoundedMomentumSignal(bars, length, kind) };
            var indicator = new MomentumOscillator(length, 3, average);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
            Assert.Equal(expected["Mo"], run[indicator].ToArray());
            Assert.Equal(expected["Signal"], run[indicator.Signal].ToArray());
            var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            var maType = ((IBuiltInMovingAverage)average).AvgType;
            data.CalculateMomentumOscillator(maType, length);
            Assert.Equal(expected["Mo"], data.OutputValues["Mo"]);
            Assert.Equal(expected["Signal"], data.OutputValues["Signal"]);
            var builtIn = (IBuiltInIndicator)indicator;
            var spec = IndicatorSpecs.Create(builtIn.BatchName, builtIn.CreateOptions());
            foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
            {
                Assert.NotNull(state);
                using var lifetime = state as IDisposable;
                for (var replay = 0; replay < 2; replay++)
                {
                    state.Reset();
                    for (var i = 0; i < bars.Count; i++)
                    foreach (var commit in new[] { false, true })
                    {
                        var b = bars[i];
                        var native = new OhlcvBar("FAST", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                        var result = state.Update(native, commit, true);
                        Assert.Equal(expected["Mo"][i], result.Outputs!["Mo"]);
                        Assert.Equal(expected["Signal"][i], result.Outputs["Signal"]);
                    }
                }
            }
        }
    }

    [Fact]
    public async Task TypedChainingPreservesOhlcAndConfiguredPeriods()
    {
        var bars = new[] { 100d, 120, 110, 150 }.Select((v, i) =>
            new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 2, v - 3, v, 1)).ToArray();
        var source = new Sma(2);
        var indicator = new MomentumOscillator(3, 3, new Sma());
        indicator.Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(source, indicator).BuildAsync();
        var projected = run[source].ToArray().Select((v, i) =>
            new Bar(bars[i].Time, bars[i].Open, bars[i].High, bars[i].Low, v, bars[i].Volume)).ToArray();
        var expected = new Dictionary<string, double[]> { ["Mo"] = BuiltInFormulaReferences.RoundedMomentum(projected, 3), ["Signal"] = BuiltInFormulaReferences.RoundedMomentumSignal(projected, 3, 1) };
        Assert.Equal(expected["Mo"], run[indicator].ToArray());
        Assert.Equal(expected["Signal"], run[indicator.Signal].ToArray());
        var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
            bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
        data.InputValues = new() { 1, 2, 4, 8 };
        using var context = new OoplesFinance.StockIndicators.Builder.Compute.ComputeContext();
        using var result = OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.ComputeMomentumOscillatorFast(data, context, 1);
        Assert.Equal(new[] { 0d, 200, 200, 200 }, result.ToArray());
    }
}

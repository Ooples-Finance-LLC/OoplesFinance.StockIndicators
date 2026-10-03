using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class GuppyNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(GuppyMultipleMovingAverage)).Select(c => new object[] { c });

    public static IEnumerable<object[]> DistanceCases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(GuppyDistanceIndicator)).Select(c => new object[] { c });

    [Theory, MemberData(nameof(DistanceCases))]
    public Task EveryDistanceConfigurationReceivesEveryNumericalClass(IndicatorValidationCase testCase)
        => EveryConfigurationReceivesEveryNumericalClass(testCase);

    [Theory, MemberData(nameof(DistanceCases))]
    public void EveryDistanceRouteMatchesIndependentRibbonFormula(IndicatorValidationCase testCase)
        => EveryRouteMatchesIndependentRibbonFormula(testCase);

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Theory, MemberData(nameof(Cases))]
    public void EveryRouteMatchesIndependentRibbonFormula(IndicatorValidationCase testCase)
    {
        var builtIn = (IBuiltInIndicator)testCase.Factory();
        var options = builtIn.CreateOptions();
        var spec = new IndicatorSpec(builtIn.BatchName, options);
        Assert.True(BuilderArmBinding.TryGetTarget(options.GetType(), out var target));
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        {
            var expected = builtIn.BatchName == IndicatorName.GuppyDistanceIndicator
                ? BuiltInFormulaReferences.RoundedGuppyDistance(fixture.Bars, options) : BuiltInFormulaReferences.RoundedGuppy(fixture.Bars, options);
            var count = expected.Values.Select(values => Array.FindIndex(values, double.IsInfinity))
                .Where(i => i >= 0).DefaultIfEmpty(fixture.Bars.Count).Min();
            var bars = fixture.Bars.Take(count).ToArray();
            StockData Data() => new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            foreach (var key in expected.Keys)
            {
                var outputSpec = new IndicatorSpec(builtIn.BatchName, options, key);
                Assert.Equal(expected[key].Take(count), BuilderArmBinding.Compute(Data(), outputSpec, target));
                using var context = new ComputeContext();
                using var buffer = IndicatorCompute.ComputeArm(Data(), outputSpec, context);
                Assert.NotNull(buffer);
                Assert.Equal(expected[key].Take(count), buffer.Value.ToArray());
            }
            foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
            {
                Assert.NotNull(state);
                using var lifetime = state as IDisposable;
                for (var replay = 0; replay < 2; replay++)
                {
                    state.Reset();
                    for (var i = 0; i < bars.Length; i++)
                    {
                        var b = bars[i];
                        var native = new OhlcvBar("GUPPY", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                        foreach (var commit in new[] { false, true })
                        {
                            var outputs = state.Update(native, commit, true).Outputs!;
                            foreach (var key in expected.Keys) Assert.Equal(expected[key][i], outputs[key]);
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void RawSmoothingUsesAvailableHistoryAndSignalStillUsesRawOscillator()
    {
        var bars = Enumerable.Range(0, 60).Select(i =>
            new Bar(DateTime.UnixEpoch.AddDays(i), 10, 20, 1, 5 + i % 7, 1)).ToArray();
        var options = new GuppyMultipleMovingAverageSpecOptions(length25: 46, length26: 49, length27: 50);
        var expected = BuiltInFormulaReferences.RoundedGuppy(bars, options);
        var raw = expected["SuperGmmaOsc"];
        var smoothed = raw.Select((_, i) =>
        {
            var values = raw.Skip(Math.Max(0, i - 2)).Take(Math.Min(3, i + 1)).ToArray();
            return (values.Aggregate(new ReferenceFraction(0), (sum, value) => sum + ReferenceFraction.FromDouble(value)) /
                new ReferenceFraction(values.Length)).ToDouble();
        }).ToArray();
        StockData Data() => new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
            bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
        var legacy = Data().CalculateGuppyMultipleMovingAverage(smoothLength: 3);
        Assert.Equal(smoothed, legacy.OutputValues["SuperGmmaOsc"]);
        Assert.Equal(expected["SuperGmmaSignal"], legacy.OutputValues["SuperGmmaSignal"]);
        using var context = new ComputeContext();
        using var buffer = IndicatorCompute.ComputeGuppyMultipleMaFast(Data(), context, smoothLength: 3);
        Assert.Equal(smoothed, buffer.ToArray());
        using var state = new GuppyMultipleMovingAverageState(smoothLength: 3);
        for (var i = 0; i < bars.Length; i++)
        {
            var b = bars[i];
            var native = new OhlcvBar("GUPPY", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
            foreach (var commit in new[] { false, true })
            {
                var outputs = state.Update(native, commit, true).Outputs!;
                Assert.Equal(smoothed[i], outputs["SuperGmmaOsc"]);
                Assert.Equal(expected["SuperGmmaSignal"][i], outputs["SuperGmmaSignal"]);
            }
        }
    }

    [Fact]
    public async Task SelectedInputReachesBothGuppyFamilies()
    {
        var bars = Enumerable.Range(0, 40).Select(i =>
            new Bar(DateTime.UnixEpoch.AddDays(i), 10, 20, 1, 5 + i % 7, 1)).ToArray();
        var source = new Sma(2);
        var ribbon = new GuppyMultipleMovingAverage().Of(source);
        var distance = new GuppyDistanceIndicator().Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(source, ribbon, distance).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray();
        foreach (var indicator in new IIndicator[] { ribbon, distance })
        {
            var options = ((IBuiltInIndicator)indicator).CreateOptions();
            var expected = indicator == ribbon ? BuiltInFormulaReferences.RoundedGuppy(projected, options)
                : BuiltInFormulaReferences.RoundedGuppyDistance(projected, options);
            var keys = indicator == ribbon ? new[] { "SuperGmmaOsc", "SuperGmmaSignal" } : new[] { "FastDistance", "SlowDistance" };
            for (var slot = 0; slot < keys.Length; slot++) Assert.Equal(expected[keys[slot]], run[indicator.Outputs[slot]].ToArray());
        }
    }

    [Fact]
    public void RibbonsPreserveCancellationScaleAndTinyDenominators()
    {
        Assert.Equal(1d / 3, GuppyRibbonArithmetic.Mean(new[] { double.MaxValue, 1d, -double.MaxValue }));
        foreach (var magnitude in new[] { double.Epsilon, 1d, double.MaxValue })
        {
            Assert.Equal(magnitude, GuppyRibbonArithmetic.Mean(Enumerable.Repeat(magnitude, 16).ToArray()));
            Assert.Equal(0, GuppyRibbonArithmetic.Distance(magnitude, magnitude, magnitude, magnitude, magnitude, magnitude));
            Assert.Equal(0, GuppyRibbonArithmetic.Percent(magnitude, magnitude));
            Assert.Equal(-200, GuppyRibbonArithmetic.Percent(-magnitude, magnitude));
        }
        Assert.Equal(100, GuppyRibbonArithmetic.Percent(2 * double.Epsilon, double.Epsilon));
        Assert.Equal(0, GuppyRibbonArithmetic.Percent(1, 0));
        Assert.Equal(double.PositiveInfinity, GuppyRibbonArithmetic.Percent(double.MaxValue, double.Epsilon));
        Assert.Equal(5 * double.Epsilon, GuppyRibbonArithmetic.Distance(0, double.Epsilon, 0, double.Epsilon, 0, double.Epsilon));
        Assert.Equal(double.PositiveInfinity, GuppyRibbonArithmetic.Distance(-double.MaxValue, double.MaxValue, 0, 0, 0, 0));
        using var mean = new ExactPartialMeanWindow(2);
        Assert.Equal(double.MaxValue, mean.Next(double.MaxValue, true));
        Assert.Equal(0, mean.Next(-double.MaxValue, false));
        Assert.Equal(double.MaxValue, mean.Next(double.MaxValue, true));
        Assert.Equal(0, mean.Next(-double.MaxValue, true));
        Assert.Equal(-double.MaxValue, mean.Next(-double.MaxValue, true));
    }
}

using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class PvoNumericalTests
{
    [Fact]
    public void RawEmaRatioRemainsFiniteThroughAnOverflowingDifference()
    {
        var prices = new[] { -double.MaxValue, -double.MaxValue, double.MaxValue, 7, 7, 7 };
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, v)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedPvo(bars, new PercentageVolumeOscillatorSpecOptions(1, 3, 2));
        var actual = new double[prices.Length];
        Core.OscillatorCore.PercentageVolumeOscillator(prices, actual, 1, 3);
        Assert.Equal(expected["Pvo"], actual);
        Assert.True(double.IsFinite(actual[2]));
        Assert.True(actual[2] < -399 && actual[2] > -401);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task SelectedInputControlsAllAliasesAndOutputs(int alias)
    {
        var bars = IndicatorAdversarialCases.Generate(40, 244).Single(f => f.Name.EndsWith("/negative")).Bars;
        var source = new Sma(2);
        IIndicator indicator = alias == 0 ? new Pvo(2, signalLength: 2).Of(source)
            : new PercentageVolumeOscillator(2, 4, 2).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, b.Close, selected[i])).ToArray();
        var builtIn = (IBuiltInIndicator)indicator;
        var expected = BuiltInFormulaReferences.RoundedPvo(projected, builtIn.CreateOptions());
        var keys = new[] { "Pvo", "Signal", "Histogram" };
        for (var slot = 0; slot < indicator.Outputs.Count; slot++) Assert.Equal(expected[keys[slot]], run[indicator.Outputs[slot]].ToArray());
    }

    [Fact]
    public void OscillatorOverflowRemainsAnOutputFailureAndPreviewDoesNotPoisonSignal()
    {
        var prices = new[] { -double.MaxValue, double.Epsilon, -double.MaxValue };
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, v)).ToArray();
        StockData Data() => new StockData(prices, prices, prices, prices, prices, bars.Select(b => b.Time));
        var legacy = Data().CalculatePercentageVolumeOscillator(fastLength: 2, slowLength: 1, signalLength: 2);
        var options = new PercentageVolumeOscillatorSpecOptions(2, 1, 2);
        foreach (var key in new[] { "Pvo", "Signal", "Histogram" })
        {
            var expected = key == "Pvo" ? double.NegativeInfinity : double.NaN;
            Assert.Equal(expected, legacy.OutputValues[key][1]);
            using var context = new ComputeContext();
            using var buffer = IndicatorCompute.TryComputeFast(Data(), new IndicatorSpec(Enums.IndicatorName.PercentageVolumeOscillator, options, key), context);
            Assert.NotNull(buffer);
            Assert.Equal(expected, buffer.Value.ToArray()[1]);
        }
        OhlcvBar Native(int i) { var b = bars[i]; return new OhlcvBar("PVO", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true); }
        using var state = new PercentageVolumeOscillatorState(fastLength: 2, slowLength: 1, signalLength: 2);
        state.Update(Native(0), true, true);
        Assert.Equal(double.NegativeInfinity, state.Update(Native(1), false, true).Value);
        Assert.Equal(0, state.Update(Native(2), false, true).Outputs!["Signal"]);
        Assert.Equal(double.NegativeInfinity, state.Update(Native(1), true, true).Value);
        Assert.True(double.IsNaN(state.Update(Native(2), true, true).Outputs!["Signal"]));
        state.Reset();
        Assert.Equal(0, state.Update(Native(0), true, true).Outputs!["Signal"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void VolumeIsDefaultAndChainedValuesOverrideIt(bool chained)
    {
        var bars = IndicatorAdversarialCases.Generate(32, 244).Single(f => f.Name.EndsWith("/negative")).Bars;
        var selected = bars.Select((b, i) => (double)(i * i + 3)).ToArray();
        var options = new PercentageVolumeOscillatorSpecOptions(2, 4, 2);
        var projected = chained ? bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, b.Close, selected[i])).ToArray() : bars.ToArray();
        var expected = BuiltInFormulaReferences.RoundedPvo(projected, options);
        StockData Data()
        {
            var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            if (chained) data.CustomValuesList = selected.ToList(); else data.InputValues = selected.ToList();
            return data;
        }
        var legacy = Data().CalculatePercentageVolumeOscillator(fastLength: 2, slowLength: 4, signalLength: 2);
        foreach (var key in expected.Keys)
        {
            Assert.Equal(expected[key], legacy.OutputValues[key]);
            using var context = new ComputeContext();
            using var buffer = IndicatorCompute.TryComputeFast(Data(), new IndicatorSpec(Enums.IndicatorName.PercentageVolumeOscillator, options, key), context);
            Assert.NotNull(buffer);
            Assert.Equal(expected[key], buffer.Value.ToArray());
        }
    }

    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(Pvo) || c.IndicatorType == typeof(PercentageVolumeOscillator)).Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Theory, MemberData(nameof(Cases))]
    public void EveryRouteMatchesIndependentPvoStages(IndicatorValidationCase testCase)
    {
        var builtIn = (IBuiltInIndicator)testCase.Factory();
        var options = builtIn.CreateOptions();
        var spec = new IndicatorSpec(builtIn.BatchName, options);
        Assert.True(BuilderArmBinding.TryGetTarget(options.GetType(), out var target));
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        {
            var expected = BuiltInFormulaReferences.RoundedPvo(fixture.Bars, options);
            var count = fixture.Bars.Count;
            for (var i = 0; i < count; i++)
                if (expected.Values.Any(values => !double.IsFinite(values[i]))) { count = i; break; }
            var bars = fixture.Bars.Take(count).ToArray();
            StockData Data() => new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            foreach (var key in expected.Keys)
            {
                var outputSpec = new IndicatorSpec(builtIn.BatchName, options, key);
                Assert.Equal(expected[key].Take(count), BuilderArmBinding.Compute(Data(), outputSpec, target));
                using var context = new ComputeContext();
                using var buffer = IndicatorCompute.TryComputeFast(Data(), outputSpec, context);
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
                        var native = new OhlcvBar("PVO", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
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

}

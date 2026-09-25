using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class MacdNumericalTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task SelectedInputControlsAllAliasesAndOutputs(int alias)
    {
        var bars = IndicatorAdversarialCases.Generate(40, 244).Single(f => f.Name.EndsWith("/negative")).Bars;
        var source = new Sma(2);
        IIndicator indicator = alias == 0 ? new Macd(2, 4, 2).Of(source)
            : alias == 1 ? new MacdLine(2, 4).Of(source)
            : alias == 2 ? new MacdSignal(2, 4, 2).Of(source) : new MacdHistogram(2, 4, 2).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, selected[i], selected[i], selected[i], selected[i], b.Volume)).ToArray();
        var builtIn = (IBuiltInIndicator)indicator;
        var expected = BuiltInFormulaReferences.RoundedMacd(projected, builtIn.CreateOptions());
        var keys = indicator.Outputs.Count == 1 ? new[] { builtIn.BatchOutputKey ?? "Macd" } : new[] { "Macd", "Signal", "Histogram" };
        for (var slot = 0; slot < indicator.Outputs.Count; slot++) Assert.Equal(expected[keys[slot]], run[indicator.Outputs[slot]].ToArray());
    }

    [Fact]
    public void OscillatorOverflowRemainsAnOutputFailureAndPreviewDoesNotPoisonSignal()
    {
        var prices = new[] { -double.MaxValue, -double.MaxValue, double.MaxValue, -double.MaxValue };
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 1)).ToArray();
        StockData Data() => new StockData(prices, prices, prices, prices, prices.Select(_ => 1d), bars.Select(b => b.Time));
        var legacy = Data().CalculateMovingAverageConvergenceDivergence(fastLength: 1, slowLength: 10, signalLength: 2);
        var options = new MacdSpecOptions(1, 10, 2);
        foreach (var key in new[] { "Macd", "Signal", "Histogram" })
        {
            var expected = key == "Macd" ? double.PositiveInfinity : double.NaN;
            Assert.Equal(expected, legacy.OutputValues[key][2]);
            using var context = new ComputeContext();
            using var buffer = IndicatorCompute.TryComputeFast(Data(), new IndicatorSpec(Enums.IndicatorName.MovingAverageConvergenceDivergence, options, key), context);
            Assert.NotNull(buffer);
            Assert.Equal(expected, buffer.Value.ToArray()[2]);
        }
        OhlcvBar Native(int i) { var b = bars[i]; return new OhlcvBar("MACD", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true); }
        var state = new MovingAverageConvergenceDivergenceState(fastLength: 1, slowLength: 10, signalLength: 2);
        state.Update(Native(0), true, true);
        state.Update(Native(1), true, true);
        Assert.Equal(double.PositiveInfinity, state.Update(Native(2), false, true).Value);
        Assert.Equal(0, state.Update(Native(3), false, true).Outputs!["Signal"]);
        Assert.Equal(double.PositiveInfinity, state.Update(Native(2), true, true).Value);
        Assert.True(double.IsNaN(state.Update(Native(3), true, true).Outputs!["Signal"]));
        state.Reset();
        Assert.Equal(0, state.Update(Native(0), true, true).Outputs!["Signal"]);
    }

    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(Macd) || c.IndicatorType == typeof(MacdLine) || c.IndicatorType == typeof(MacdSignal) || c.IndicatorType == typeof(MacdHistogram)).Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Theory, MemberData(nameof(Cases))]
    public void EveryRouteMatchesIndependentMacdStages(IndicatorValidationCase testCase)
    {
        var builtIn = (IBuiltInIndicator)testCase.Factory();
        var options = builtIn.CreateOptions();
        var spec = new IndicatorSpec(builtIn.BatchName, options);
        Assert.True(BuilderArmBinding.TryGetTarget(options.GetType(), out var target));
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        {
            var expected = BuiltInFormulaReferences.RoundedMacd(fixture.Bars, options);
            var count = fixture.Bars.Count;
            for (var i = 0; i < count; i++)
                if (expected.Values.Any(values => !double.IsFinite(values[i]))) { count = i; break; }
            var bars = fixture.Bars.Take(count).ToArray();
            StockData Data() => new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            var publishedKeys = testCase.Factory().Outputs.Count == 1 ? new[] { builtIn.BatchOutputKey ?? "Macd" } : expected.Keys.ToArray();
            foreach (var key in publishedKeys)
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
                        var native = new OhlcvBar("MACD", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
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

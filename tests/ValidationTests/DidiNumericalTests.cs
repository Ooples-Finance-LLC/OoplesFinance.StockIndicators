using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class DidiNumericalTests
{
    [Fact]
    public async Task SelectedInputControlsAllOutputs()
    {
        var bars = IndicatorAdversarialCases.Generate(40, 244).Single(f => f.Name.EndsWith("/negative")).Bars;
        var source = new Sma(2);
        IIndicator indicator = new DidiIndex(2, 4, 6).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, selected[i], selected[i], selected[i], selected[i], b.Volume)).ToArray();
        var builtIn = (IBuiltInIndicator)indicator;
        var expected = BuiltInFormulaReferences.RoundedDidi(projected, builtIn.CreateOptions());
        var keys = indicator.Outputs.Count == 1 ? new[] { builtIn.BatchOutputKey ?? "Curta" } : new[] { "Curta", "Media", "Longa" };
        for (var slot = 0; slot < indicator.Outputs.Count; slot++) Assert.Equal(expected[keys[slot]], run[indicator.Outputs[slot]].ToArray());
        foreach (var key in expected.Keys)
        {
            var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            data.CustomValuesList = selected.ToList();
            using var context = new ComputeContext();
            using var buffer = IndicatorCompute.TryComputeFast(data, new IndicatorSpec(builtIn.BatchName, builtIn.CreateOptions(), key), context);
            Assert.NotNull(buffer);
            Assert.Equal(expected[key], buffer.Value.ToArray());
        }
    }

    [Fact]
    public void CorePublishesCurtaIndependentlyOfLongaAndAvoidsMeanOverflow()
    {
        Core.MovingAverageCore.DidiIndex(Array.Empty<double>(), Array.Empty<double>(), int.MaxValue, int.MaxValue, int.MaxValue);
        var actual = new double[3];
        Core.MovingAverageCore.DidiIndex(new[] { 1d, 2, 4 }, actual, 1, 2, 3);
        Assert.Equal(new[] { 0d, 4d / 3, 4d / 3 }, actual);
        var values = new[] { -double.MaxValue, -double.MaxValue, double.MaxValue };
        var bars = values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 1)).ToArray();
        Core.MovingAverageCore.DidiIndex(values, actual, 1, 3, 2);
        Assert.Equal(BuiltInFormulaReferences.RoundedDidi(bars, new DidiIndexSpecOptions(1, 3, 2))["Curta"], actual);
        Assert.True(double.IsFinite(actual[2]));
    }

    private sealed class ScaledAverage(double scale) : IndicatorBase, IMovingAverage
    {
        protected internal override object? CreateState() => new State(scale);
        private sealed class State(double scale) : IIndicatorState
        {
            public void Reset() { }
            public double Update(in Bar bar) => bar.Close * scale;
        }
    }

    [Fact]
    public async Task EachCustomerAverageReceivesItsOwnStage()
    {
        var bars = Enumerable.Range(1, 12).Select(i => new Bar(DateTime.UnixEpoch.AddDays(i), i, i, i, i, 1)).ToArray();
        var indicator = new DidiIndex(1, 2, 3, new ScaledAverage(3), new ScaledAverage(2), new ScaledAverage(4));
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(bars.Select(_ => 2d / 3), run[indicator.Outputs[0]].ToArray());
        Assert.Equal(bars.Select(_ => 1d), run[indicator.Outputs[1]].ToArray());
        Assert.Equal(bars.Select(_ => 4d / 3), run[indicator.Outputs[2]].ToArray());
    }

    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(DidiIndex)).Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Theory, MemberData(nameof(Cases))]
    public void EveryRouteMatchesIndependentDidiMeans(IndicatorValidationCase testCase)
    {
        var builtIn = (IBuiltInIndicator)testCase.Factory();
        var options = builtIn.CreateOptions();
        var spec = new IndicatorSpec(builtIn.BatchName, options);
        Assert.True(BuilderArmBinding.TryGetTarget(options.GetType(), out var target));
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        {
            var expected = BuiltInFormulaReferences.RoundedDidi(fixture.Bars, options);
            var count = fixture.Bars.Count;
            for (var i = 0; i < count; i++)
                if (expected.Values.Any(values => !double.IsFinite(values[i]))) { count = i; break; }
            var bars = fixture.Bars.Take(count).ToArray();
            StockData Data() => new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            var publishedKeys = testCase.Factory().Outputs.Count == 1 ? new[] { builtIn.BatchOutputKey ?? "Curta" } : expected.Keys.ToArray();
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

using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class LindaNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(LindaRaschke310Oscillator)).Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High),
        bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));

    private static OhlcvBar Native(Bar b) => new("LINDA", BarTimeframe.Minutes(1), b.Time, b.Time,
        b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Theory, MemberData(nameof(Cases))]
    public void EveryRouteMatchesSixIndependentOutputs(IndicatorValidationCase testCase)
    {
        var builtIn = (IBuiltInIndicator)testCase.Factory();
        var options = builtIn.CreateOptions();
        var spec = new IndicatorSpec(builtIn.BatchName, options);
        Assert.True(BuilderArmBinding.TryGetTarget(options.GetType(), out var target));
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        {
            var bars = fixture.Bars;
            var expected = BuiltInFormulaReferences.RoundedLinda(bars, options);
            foreach (var key in expected.Keys)
            {
                var outputSpec = new IndicatorSpec(builtIn.BatchName, options, key);
                Assert.Equal(expected[key], BuilderArmBinding.Compute(Data(bars), outputSpec, target));
                using var context = new ComputeContext();
                using var buffer = IndicatorCompute.TryComputeFast(Data(bars), outputSpec, context);
                Assert.NotNull(buffer);
                Assert.Equal(expected[key], buffer.Value.ToArray());
            }
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
                        var result = state.Update(Native(bars[i]), commit, true);
                        foreach (var key in expected.Keys) Assert.Equal(expected[key][i], result.Outputs![key]);
                    }
                }
            }
        }
    }

    [Fact]
    public async Task SelectedInputControlsAllSixOutputs()
    {
        var bars = IndicatorAdversarialCases.Generate(40, 244).Single(f => f.Name.EndsWith("/negative")).Bars;
        var source = new Sma(2);
        var indicator = new LindaRaschke310Oscillator(2, 4, 3).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, selected[i], selected[i], selected[i], selected[i], b.Volume)).ToArray();
        var builtIn = (IBuiltInIndicator)indicator;
        var expected = BuiltInFormulaReferences.RoundedLinda(projected, builtIn.CreateOptions());
        var slot = 0;
        foreach (var (key, values) in expected)
        {
            Assert.Equal(values, run[indicator.Outputs[slot++]].ToArray());
            var data = Data(bars);
            data.CustomValuesList = selected.ToList();
            using var context = new ComputeContext();
            using var buffer = IndicatorCompute.TryComputeFast(data, new IndicatorSpec(builtIn.BatchName, builtIn.CreateOptions(), key), context);
            Assert.NotNull(buffer);
            Assert.Equal(values, buffer.Value.ToArray());
        }
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
    public async Task FourCustomerAveragesKeepTheirPublishedOrder()
    {
        var bars = new[] { new Bar(DateTime.UnixEpoch, 10, 10, 10, 10, 1) };
        var indicator = new LindaRaschke310Oscillator(2, 4, 3,
            new ScaledAverage(4), new ScaledAverage(2), new ScaledAverage(3), new ScaledAverage(5));
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        var expected = new[] { 20d, 60, -40, 100, 500, -400 };
        for (var i = 0; i < expected.Length; i++) Assert.Equal(expected[i], run[indicator.Outputs[i]].ToArray()[0]);
    }

    [Fact]
    public void CorePublishesRawDifferenceAndHandlesEmptyMaximumPeriods()
    {
        Core.OscillatorCore.LindaRaschke310Oscillator(Array.Empty<double>(), Array.Empty<double>(), int.MaxValue, int.MaxValue, int.MaxValue);
        var values = new[] { 1d, 2, 4, -double.MaxValue, double.MaxValue };
        var bars = values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 1)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedLinda(bars, new LindaRaschke310OscillatorSpecOptions(1, 3, 2));
        var output = new double[values.Length];
        Core.OscillatorCore.LindaRaschke310Oscillator(values, output, 1, 3, 2);
        Assert.Equal(expected["LindaMacd"], output);
        // The slow arithmetic mean publishes zero until its full window is available.
        Assert.Equal(2, output[1]);
    }

    [Fact]
    public void RatioOverflowDoesNotPoisonDifferenceSignalOrCommitPreview()
    {
        Bar B(double v) => new(DateTime.UnixEpoch, v, v, v, v, 1);
        using var state = new LindaRaschke3_10OscillatorState(fastLength: 2, slowLength: 1, smoothLength: 1);
        var stable = Native(B(-double.MaxValue));
        var overflow = Native(B(double.Epsilon));
        state.Update(stable, true, true);
        Assert.True(double.IsNegativeInfinity(state.Update(overflow, false, true).Outputs!["LindaPpo"]));
        Assert.Equal(0, state.Update(stable, false, true).Outputs!["LindaPpoSignal"]);
        var committed = state.Update(overflow, true, true).Outputs!;
        Assert.True(double.IsFinite(committed["LindaMacdSignal"]));
        Assert.True(double.IsNaN(committed["LindaPpoSignal"]));
        Assert.True(double.IsNaN(state.Update(stable, true, true).Outputs!["LindaPpoSignal"]));
        state.Reset();
        Assert.Equal(-100, state.Update(stable, true, true).Outputs!["LindaPpoSignal"]);
    }
}

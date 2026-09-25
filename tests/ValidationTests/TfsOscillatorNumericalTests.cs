using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class TfsOscillatorNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(TFSMboIndicator) || c.IndicatorType == typeof(TFSMboPercentagePriceOscillator))
        .Select(c => new object[] { c });

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

    private static OhlcvBar Native(Bar b) => new("DINAPOLI", BarTimeframe.Minutes(1), b.Time, b.Time,
        b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Theory, MemberData(nameof(Cases))]
    public void EveryRouteMatchesIndependentTfsStages(IndicatorValidationCase testCase)
    {
        var builtIn = (IBuiltInIndicator)testCase.Factory();
        var options = builtIn.CreateOptions();
        var percentage = builtIn.BatchName == IndicatorName.TFSMboPercentagePriceOscillator;
        var spec = new IndicatorSpec(builtIn.BatchName, options);
        Assert.True(BuilderArmBinding.TryGetTarget(options.GetType(), out var target));
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
        {
            var bars = fixture.Bars;
            var expected = BuiltInFormulaReferences.RoundedTfsOscillator(bars, options, percentage);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SelectedInputControlsEveryOutput(bool percentage)
    {
        var bars = IndicatorAdversarialCases.Generate(256, 244).Single(f => f.Name.EndsWith("/negative")).Bars;
        var source = new Sma(2);
        IIndicator indicator = percentage ? new TFSMboPercentagePriceOscillator(2).Of(source)
            : new TFSMboIndicator(2, 3).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, Math.Max(b.High, selected[i]),
            Math.Min(b.Low, selected[i]), selected[i], b.Volume)).ToArray();
        var builtIn = (IBuiltInIndicator)indicator;
        var expected = BuiltInFormulaReferences.RoundedTfsOscillator(projected, builtIn.CreateOptions(), percentage);
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

    [Fact]
    public void CoreUsesFullWindowArithmeticMeansAndExactPercentage()
    {
        Core.OscillatorCore.TFSMboPercentagePriceOscillator(Array.Empty<double>(), Array.Empty<double>(), int.MaxValue, int.MaxValue);
        var output = new double[3];
        Core.OscillatorCore.TFSMboPercentagePriceOscillator(new[] { 4d, 0, 2 }, output, 1, 3);
        Assert.Equal(new[] { 0d, 0, 0 }, output);
        Core.OscillatorCore.TFSMboPercentagePriceOscillator(new[] { double.MaxValue, double.MaxValue, -double.MaxValue }, output, 1, 3);
        var bars = new[] { double.MaxValue, double.MaxValue, -double.MaxValue }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 1)).ToArray();
        Assert.Equal(BuiltInFormulaReferences.RoundedTfsOscillator(bars, new TFSMboIndicatorSpecOptions(1, 3, 1), true)["Ppo"], output);
        Assert.True(double.IsFinite(output[2]));
        Assert.InRange(output[2], -401, -399);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CustomerAveragesReceiveFastSlowSignalOrder(bool percentage)
    {
        var bars = Enumerable.Range(1, 12).Select(i => new Bar(DateTime.UnixEpoch.AddDays(i), i, i, i, i, 1)).ToArray();
        IIndicator indicator = percentage ? new TFSMboPercentagePriceOscillator(2, new ScaledAverage(2), new ScaledAverage(4), new ScaledAverage(3))
            : new TFSMboIndicator(1, 2, 3, new ScaledAverage(2), new ScaledAverage(4), new ScaledAverage(3));
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        var line = bars.Select(b => percentage ? -50 : -2 * b.Close).ToArray();
        Assert.Equal(line, run[indicator.Outputs[0]].ToArray());
        Assert.Equal(line.Select(v => 3 * v), run[indicator.Outputs[1]].ToArray());
        Assert.Equal(line.Select(v => -2 * v), run[indicator.Outputs[2]].ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OverflowPreviewIsIsolatedAndResetRestoresSignal(bool percentage)
    {
        OhlcvBar B(double v) => Native(new Bar(DateTime.UnixEpoch, v, v, v, v, 1));
        IStreamingIndicatorState state = percentage ? new TFSMboPercentagePriceOscillatorState(fastLength: 2, slowLength: 3, signalLength: 1)
            : new TFSMboIndicatorState(fastLength: 1, slowLength: 3, signalLength: 1);
        using var lifetime = state as IDisposable;
        state.Update(B(double.MaxValue), true, true);
        state.Update(B(percentage ? -double.MaxValue : double.MaxValue), true, true);
        if (!percentage) state.Update(B(double.MaxValue), true, true);
        var overflow = B(percentage ? 3 : -double.MaxValue);
        Assert.True(double.IsInfinity(state.Update(overflow, false, true).Value));
        Assert.True(double.IsFinite(state.Update(B(double.MaxValue), false, true).Outputs!["Signal"]));
        state.Update(overflow, true, true);
        Assert.True(double.IsNaN(state.Update(B(double.MaxValue), true, true).Outputs!["Signal"]));
        state.Reset();
        Assert.True(double.IsFinite(state.Update(B(1), true, true).Outputs!["Signal"]));
    }
}

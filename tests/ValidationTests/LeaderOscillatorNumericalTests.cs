using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class LeaderOscillatorNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(MovingAverageConvergenceDivergenceLeader) || c.IndicatorType == typeof(PercentagePriceOscillatorLeader))
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
    public void EveryRouteMatchesIndependentLeaderStages(IndicatorValidationCase testCase)
    {
        var builtIn = (IBuiltInIndicator)testCase.Factory();
        var options = builtIn.CreateOptions();
        var percentage = builtIn.BatchName == IndicatorName.PercentagePriceOscillatorLeader;
        var spec = new IndicatorSpec(builtIn.BatchName, options);
        Assert.True(BuilderArmBinding.TryGetTarget(options.GetType(), out var target));
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        {
            var bars = fixture.Bars;
            var expected = BuiltInFormulaReferences.RoundedLeaderOscillator(bars, options, percentage);
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
        var bars = IndicatorAdversarialCases.Generate(40, 244).Single(f => f.Name.EndsWith("/negative")).Bars;
        var source = new Sma(2);
        IIndicator indicator = percentage ? new PercentagePriceOscillatorLeader(2).Of(source)
            : new MovingAverageConvergenceDivergenceLeader(2, 3).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, Math.Max(b.High, selected[i]),
            Math.Min(b.Low, selected[i]), selected[i], b.Volume)).ToArray();
        var builtIn = (IBuiltInIndicator)indicator;
        var expected = BuiltInFormulaReferences.RoundedLeaderOscillator(projected, builtIn.CreateOptions(), percentage);
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
    public void CoreUsesSmoothedResidualsAndMatchesRoundedStageReference()
    {
        Core.OscillatorCore.PercentagePriceOscillatorLeader(Array.Empty<double>(), Array.Empty<double>(), int.MaxValue, int.MaxValue);
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        {
            var actual = new double[fixture.Bars.Count];
            Core.OscillatorCore.PercentagePriceOscillatorLeader(fixture.Bars.Select(b => b.Close).ToArray(), actual, 1, 3);
            Assert.Equal(BuiltInFormulaReferences.RoundedLeaderOscillator(fixture.Bars,
                new MovingAverageConvergenceDivergenceLeaderSpecOptions(1, 3), true)["Ppo"], actual);
        }
        var simple = new[] { 4d, 0, 2 };
        var output = new double[3];
        Core.OscillatorCore.PercentagePriceOscillatorLeader(simple, output, 1, 3);
        Assert.Equal(new[] { 0d, -100 }, output.Take(2));
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
    public async Task CustomerAveragesReceiveFastSlowResidualResidualSignalOrder()
    {
        var bars = Enumerable.Range(1, 12).Select(i => new Bar(DateTime.UnixEpoch.AddDays(i), i, i, i, i, 1)).ToArray();
        var indicator = new MovingAverageConvergenceDivergenceLeader(1, 2, 3, new ScaledAverage(2), new ScaledAverage(3),
            new ScaledAverage(4), new ScaledAverage(5), new ScaledAverage(6));
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(bars.Select(b => 5 * b.Close), run[indicator.Outputs[0]].ToArray());
        Assert.Equal(bars.Select(b => -2 * b.Close), run[indicator.Outputs[1]].ToArray());
        Assert.Equal(bars.Select(b => -7 * b.Close), run[indicator.Outputs[2]].ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResidualOverflowPreviewIsIsolatedAndResetRestoresState(bool percentage)
    {
        OhlcvBar B(double v) => Native(new Bar(DateTime.UnixEpoch, v, v, v, v, 1));
        IStreamingIndicatorState state = percentage ? new PercentagePriceOscillatorLeaderState(fastLength: 5, slowLength: 7)
            : new MovingAverageConvergenceDivergenceLeaderState(fastLength: 5, slowLength: 7);
        using var lifetime = state as IDisposable;
        for (var i = 0; i < 10; i++) state.Update(B(double.MaxValue), true, true);
        state.Update(B(-double.MaxValue), false, true);
        Assert.True(double.IsFinite(state.Update(B(double.MaxValue), false, true).Value));
        state.Update(B(-double.MaxValue), true, true);
        Assert.True(double.IsNaN(state.Update(B(double.MaxValue), true, true).Value));
        state.Reset();
        Assert.Equal(0, state.Update(B(1), true, true).Value);
    }
}

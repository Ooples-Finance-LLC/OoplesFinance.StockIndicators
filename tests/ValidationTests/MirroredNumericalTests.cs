using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class MirroredNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(MirroredMovingAverageConvergenceDivergence) ||
                    c.IndicatorType == typeof(MirroredPercentagePriceOscillator)).Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ArithmeticMeansMatchEveryRoute(bool percentage)
    {
        var testCase = percentage
            ? new IndicatorValidationCase(typeof(MirroredPercentagePriceOscillator), "arithmetic", () => new MirroredPercentagePriceOscillator(3, 2, new Sma(3)))
            : new IndicatorValidationCase(typeof(MirroredMovingAverageConvergenceDivergence), "arithmetic", () => new MirroredMovingAverageConvergenceDivergence(3, 2, new Sma(3)));
        EveryRouteMatchesSixIndependentOutputs(testCase);
    }

    [Fact]
    public void CoreUsesOpenAndCloseMeansWithoutArtificialSignFlips()
    {
        var open = new[] { 10d, 10, -double.MaxValue, double.MaxValue };
        var close = new[] { 20d, 5, double.MaxValue, -double.MaxValue };
        var bars = open.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, Math.Max(v, close[i]), Math.Min(v, close[i]), close[i], 1)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedMirrored(bars, new MirroredPercentagePriceOscillatorSpecOptions(1), true)["Ppo"];
        var output = new double[open.Length];
        Core.OscillatorCore.MirroredPercentagePriceOscillator(open, close, output, 1);
        Assert.Equal(new[] { 100d, -50, -200, -200 }, output);
        Assert.Equal(expected, output);
        Core.OscillatorCore.MirroredPercentagePriceOscillator(Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>(), int.MaxValue);
        Assert.Throws<ArgumentException>(() => Core.OscillatorCore.MirroredPercentagePriceOscillator(open, new double[1], output));
        Assert.Throws<ArgumentException>(() => Core.OscillatorCore.MirroredPercentagePriceOscillator(open, close, new double[1]));
    }

    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High),
        bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));

    private static OhlcvBar Native(Bar b) => new("MIRROR", BarTimeframe.Minutes(1), b.Time, b.Time,
        b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Theory, MemberData(nameof(Cases))]
    public void EveryRouteMatchesSixIndependentOutputs(IndicatorValidationCase testCase)
    {
        var builtIn = (IBuiltInIndicator)testCase.Factory();
        var options = builtIn.CreateOptions();
        var percentage = builtIn.BatchName == IndicatorName.MirroredPercentagePriceOscillator;
        var spec = new IndicatorSpec(builtIn.BatchName, options);
        Assert.True(BuilderArmBinding.TryGetTarget(options.GetType(), out var target));
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        {
            var bars = fixture.Bars;
            var expected = BuiltInFormulaReferences.RoundedMirrored(bars, options, percentage);
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
    public async Task SelectedInputPreservesOriginalOpen(bool percentage)
    {
        var bars = IndicatorAdversarialCases.Generate(40, 244).Single(f => f.Name.EndsWith("/negative")).Bars;
        var source = new Sma(2);
        IIndicator indicator = percentage ? new MirroredPercentagePriceOscillator(2, 3).Of(source)
            : new MirroredMovingAverageConvergenceDivergence(2, 3).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, Math.Max(b.High, selected[i]),
            Math.Min(b.Low, selected[i]), selected[i], b.Volume)).ToArray();
        var builtIn = (IBuiltInIndicator)indicator;
        var expected = BuiltInFormulaReferences.RoundedMirrored(projected, builtIn.CreateOptions(), percentage);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FourCustomerAveragesKeepTheirPublishedOrder(bool percentage)
    {
        var bars = new[] { new Bar(DateTime.UnixEpoch, 10, 30, 10, 30, 1) };
        IIndicator indicator = percentage
            ? new MirroredPercentagePriceOscillator(2, 3, new ScaledAverage(2), new ScaledAverage(4), new ScaledAverage(3), new ScaledAverage(5))
            : new MirroredMovingAverageConvergenceDivergence(2, 3, new ScaledAverage(2), new ScaledAverage(4), new ScaledAverage(3), new ScaledAverage(5));
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        var line = percentage ? 500d : 100d;
        var mirror = percentage ? -250d / 3 : -100d;
        var expected = new[] { line, line * 3, line - line * 3, mirror, mirror * 5, mirror - mirror * 5 };
        for (var i = 0; i < expected.Length; i++) Assert.Equal(expected[i], run[indicator.Outputs[i]].ToArray()[0]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OverflowPreviewDoesNotCommitAndMirrorSignalIsIndependent(bool percentage)
    {
        Bar B(double open, double close) => new(DateTime.UnixEpoch, open, Math.Max(open, close), Math.Min(open, close), close, 1);
        var stable = B(1, 1);
        var overflow = percentage ? B(double.Epsilon, double.MaxValue) : B(-double.MaxValue, double.MaxValue);
        using var lifetime = percentage ? (IDisposable)new MirroredPercentagePriceOscillatorState(length: 1, signalLength: 1)
            : new MirroredMovingAverageConvergenceDivergenceState(length: 1, signalLength: 1);
        var state = (IStreamingIndicatorState)lifetime;
        state.Update(Native(stable), true, true);
        Assert.True(double.IsPositiveInfinity(state.Update(Native(overflow), false, true).Value));
        Assert.Equal(0, state.Update(Native(stable), false, true).Outputs!["Signal"]);
        state.Update(Native(overflow), true, true);
        var next = state.Update(Native(stable), true, true).Outputs!;
        Assert.True(double.IsNaN(next["Signal"]));
        if (percentage) Assert.Equal(0, next["MirrorSignal"]);
        else Assert.True(double.IsNaN(next["MirrorSignal"]));
        state.Reset();
        Assert.Equal(0, state.Update(Native(stable), true, true).Outputs!["Signal"]);
    }
}

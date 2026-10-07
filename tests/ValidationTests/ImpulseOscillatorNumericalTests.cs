using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class ImpulseOscillatorNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(ImpulseMovingAverageConvergenceDivergence) || c.IndicatorType == typeof(ImpulsePercentagePriceOscillator))
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

    private static OhlcvBar Native(Bar b) => new("IMPULSE", BarTimeframe.Minutes(1), b.Time, b.Time,
        b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Theory, MemberData(nameof(Cases))]
    public void EveryRouteMatchesIndependentImpulseStages(IndicatorValidationCase testCase)
    {
        var builtIn = (IBuiltInIndicator)testCase.Factory();
        var options = builtIn.CreateOptions();
        var percentage = builtIn.BatchName == IndicatorName.ImpulsePercentagePriceOscillator;
        var spec = new IndicatorSpec(builtIn.BatchName, options);
        Assert.True(BuilderArmBinding.TryGetTarget(options.GetType(), out var target));
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        {
            var bars = fixture.Bars;
            var expected = BuiltInFormulaReferences.RoundedImpulseReference(bars, options, percentage);
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
        IIndicator indicator = percentage ? new ImpulsePercentagePriceOscillator(2).Of(source)
            : new ImpulseMovingAverageConvergenceDivergence(2, 3).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) =>
        {
            var value = selected[i];
            var within = value >= b.Low && value <= b.High;
            var previous = i == 0 ? value : selected[i - 1];
            return new Bar(b.Time, b.Open, within ? b.High : Math.Max(value, previous),
                within ? b.Low : Math.Min(value, previous), value, b.Volume);
        }).ToArray();
        var builtIn = (IBuiltInIndicator)indicator;
        var expected = BuiltInFormulaReferences.RoundedImpulseReference(bars, builtIn.CreateOptions(), percentage, selected);
        var legacyExpected = BuiltInFormulaReferences.RoundedImpulseReference(projected, builtIn.CreateOptions(), percentage, selected);
        var slot = 0;
        foreach (var (key, values) in expected)
        {
            Assert.Equal(values, run[indicator.Outputs[slot++]].ToArray());
            var data = Data(bars);
            data.CustomValuesList = selected.ToList();
            using var context = new ComputeContext();
            using var buffer = IndicatorCompute.TryComputeFast(data, new IndicatorSpec(builtIn.BatchName, builtIn.CreateOptions(), key), context);
            Assert.NotNull(buffer);
            Assert.Equal(legacyExpected[key], buffer.Value.ToArray());
        }
    }

    [Fact]
    public void CoreUsesZeroLagBreakoutAndAvailableSampleSignalStartsImmediately()
    {
        Core.OscillatorCore.ImpulsePercentagePriceOscillator(Array.Empty<double>(), Array.Empty<double>(), int.MaxValue);
        foreach (var fixture in IndicatorAdversarialCases.Generate(40, 244))
        {
            var bars = fixture.Bars.Select(b => new Bar(b.Time, b.Close, b.Close, b.Close, b.Close, b.Volume)).ToArray();
            var actual = new double[bars.Length];
            Core.OscillatorCore.ImpulsePercentagePriceOscillator(bars.Select(b => b.Close).ToArray(), actual, 2);
            Assert.Equal(BuiltInFormulaReferences.RoundedImpulseReference(bars, new ImpulsePercentagePriceOscillatorSpecOptions(2), true)["Ppo"], actual);
        }
        var simple = new[] { 1d, 2 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 1)).ToArray();
        var result = Data(simple).CalculateImpulseMovingAverageConvergenceDivergence(length: 2, signalLength: 9);
        Assert.Equal(new[] { .5, .5 }, result.OutputValues["Macd"]);
        Assert.Equal(new[] { .5, .5 }, result.OutputValues["Signal"]);
        var empty = Data(Array.Empty<Bar>());
        Assert.Empty(empty.CalculateImpulseMovingAverageConvergenceDivergence(signalLength: int.MaxValue).CustomValuesList);
        Assert.Empty(empty.CalculateImpulsePercentagePriceOscillator(signalLength: int.MaxValue).CustomValuesList);
        using var mean = new RoundedPartialMeanSmoother(2);
        Assert.Equal(double.MaxValue, mean.Next(double.MaxValue, true));
        Assert.Equal(double.MaxValue, mean.Next(double.MaxValue, false));
        Assert.Equal(0, mean.Next(-double.MaxValue, true));
        Assert.True(double.IsNaN(mean.Next(double.PositiveInfinity, false)));
        Assert.Equal(-double.MaxValue, mean.Next(-double.MaxValue, false));
        mean.Next(double.PositiveInfinity, true);
        Assert.True(double.IsNaN(mean.Next(0, true)));
        mean.Reset();
        Assert.Equal(3, mean.Next(3, true));
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
    public async Task CustomerAveragesControlOnlyHighAndLowChannelStages()
    {
        var bars = Enumerable.Range(1, 12).Select(i => new Bar(DateTime.UnixEpoch.AddDays(i), i, i, i, i, 1)).ToArray();
        var indicator = new ImpulseMovingAverageConvergenceDivergence(1, 3, new ScaledAverage(.5), new ScaledAverage(.25));
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        var line = bars.Select(b => b.Close / 2).ToArray();
        var signal = line.Select((_, i) => line.Skip(Math.Max(0, i - 2)).Take(Math.Min(3, i + 1)).Average()).ToArray();
        Assert.Equal(line, run[indicator.Outputs[0]].ToArray());
        Assert.Equal(signal, run[indicator.Outputs[1]].ToArray());
        Assert.Equal(line.Select((v, i) => v - signal[i]), run[indicator.Outputs[2]].ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MiddleOverflowPreviewDoesNotCommitInvalidSignal(bool percentage)
    {
        OhlcvBar B(double v) => Native(new Bar(DateTime.UnixEpoch, v, v, v, v, 1));
        IStreamingIndicatorState state = percentage ? new ImpulsePercentagePriceOscillatorState(length: 3)
            : new ImpulseMovingAverageConvergenceDivergenceState(length: 3);
        using var lifetime = state as IDisposable;
        for (var i = 0; i < 8; i++) state.Update(B(-double.MaxValue), true, true);
        state.Update(B(double.MaxValue), true, true);
        state.Update(B(double.MaxValue), true, true);
        Assert.True(double.IsNaN(state.Update(B(double.MaxValue), false, true).Value));
        Assert.True(double.IsFinite(state.Update(B(-double.MaxValue), false, true).Outputs!["Signal"]));
        state.Update(B(double.MaxValue), true, true);
        Assert.True(double.IsNaN(state.Update(B(-double.MaxValue), true, true).Outputs!["Signal"]));
        state.Reset();
        // The Wilder channel starts at rounded 1/3; the middle starts at 1.
        var boundary = ReferenceFraction.FromDouble(1d / 3);
        var expected = percentage ? (new ReferenceFraction(100) * (new ReferenceFraction(1) / boundary - new ReferenceFraction(1))).ToDouble()
            : (new ReferenceFraction(1) - boundary).ToDouble();
        Assert.Equal(expected, state.Update(B(1), true, true).Outputs!["Signal"]);
    }
}

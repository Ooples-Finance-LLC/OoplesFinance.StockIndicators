using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class FourOscillatorNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(_4MovingAverageConvergenceDivergence) || c.IndicatorType == typeof(_4PercentagePriceOscillator))
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

    private static OhlcvBar Native(Bar b) => new("FOUR", BarTimeframe.Minutes(1), b.Time, b.Time,
        b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Theory, MemberData(nameof(Cases))]
    public void EveryRouteMatchesSixIndependentOutputs(IndicatorValidationCase testCase)
    {
        var builtIn = (IBuiltInIndicator)testCase.Factory();
        var options = builtIn.CreateOptions();
        var percentage = builtIn.BatchName == IndicatorName._4PercentagePriceOscillator;
        var spec = new IndicatorSpec(builtIn.BatchName, options);
        Assert.True(BuilderArmBinding.TryGetTarget(options.GetType(), out var target));
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        {
            var bars = fixture.Bars;
            var expected = BuiltInFormulaReferences.RoundedFourOscillator(bars, options, percentage);
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
    public async Task SelectedInputControlsPublishedPairs(bool percentage)
    {
        var bars = IndicatorAdversarialCases.Generate(40, 244).Single(f => f.Name.EndsWith("/negative")).Bars;
        var source = new Sma(2);
        IIndicator indicator = percentage ? new _4PercentagePriceOscillator(2, 3).Of(source)
            : new _4MovingAverageConvergenceDivergence(2, 3).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, Math.Max(b.High, selected[i]),
            Math.Min(b.Low, selected[i]), selected[i], b.Volume)).ToArray();
        var builtIn = (IBuiltInIndicator)indicator;
        var expected = BuiltInFormulaReferences.RoundedFourOscillator(projected, builtIn.CreateOptions(), percentage);
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
    public async Task AllTenCustomerStagesRetainTheirPublishedOrder(bool percentage)
    {
        var averages = Enumerable.Range(1, 10).Select(i => new ScaledAverage(i)).ToArray();
        var bars = new[] { new Bar(DateTime.UnixEpoch, 10, 10, 10, 10, 1) };
        IIndicator indicator = percentage
            ? new _4PercentagePriceOscillator(maType: averages[0], secondAverage: averages[1], thirdAverage: averages[2], fourthAverage: averages[3], fifthAverage: averages[4], average6Average: averages[5], average7Average: averages[6], average8Average: averages[7], average9Average: averages[8], average10Average: averages[9])
            : new _4MovingAverageConvergenceDivergence(maType: averages[0], secondAverage: averages[1], thirdAverage: averages[2], fourthAverage: averages[3], fifthAverage: averages[4], average6Average: averages[5], average7Average: averages[6], average8Average: averages[7], average9Average: averages[8], average10Average: averages[9]);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        var first = percentage ? -200d / 3 : -20d;
        var second = percentage ? 100d : 20d;
        var expected = new[] { first, first * 10, first - first * 10, second, second * 8, second - second * 8 };
        for (var i = 0; i < expected.Length; i++) Assert.Equal(expected[i], run[indicator.Outputs[i]].ToArray()[0]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ArithmeticMeanAndEmptyRoutes(bool percentage)
    {
        var testCase = percentage
            ? new IndicatorValidationCase(typeof(_4PercentagePriceOscillator), "arithmetic", () => new _4PercentagePriceOscillator(3, 4, 2, 5, maType: new Sma(3)))
            : new IndicatorValidationCase(typeof(_4MovingAverageConvergenceDivergence), "arithmetic", () => new _4MovingAverageConvergenceDivergence(3, 4, 2, 5, maType: new Sma(3)));
        EveryRouteMatchesSixIndependentOutputs(testCase);
        var builtIn = (IBuiltInIndicator)testCase.Factory();
        using var context = new ComputeContext();
        using var result = IndicatorCompute.TryComputeFast(Data(Array.Empty<Bar>()), new IndicatorSpec(builtIn.BatchName, builtIn.CreateOptions()), context);
        Assert.NotNull(result);
        Assert.Empty(result.Value.ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OverflowPreviewDoesNotPoisonIndependentPublishedPair(bool percentage)
    {
        Bar B(double v) => new(DateTime.UnixEpoch, v, v, v, v, 1);
        using var lifetime = percentage ? (IDisposable)new _4PercentagePriceOscillatorState(length1: 2, length2: 1, length3: 1, length4: 1)
            : new _4MovingAverageConvergenceDivergenceState(length1: 1, length2: 1, length3: 5, length4: 1);
        var state = (IStreamingIndicatorState)lifetime;
        var stable = Native(B(percentage ? -double.MaxValue : double.MaxValue));
        var overflow = Native(B(percentage ? double.Epsilon : -double.MaxValue));
        // Complete the available-sample EMA startup before constructing the overflow event.
        for (var i = 0; i < (percentage ? 2 : 5); i++) state.Update(stable, true, true);
        Assert.True(double.IsNegativeInfinity(state.Update(overflow, false, true).Value));
        Assert.Equal(0, state.Update(stable, false, true).Outputs!["Signal1"]);
        var committed = state.Update(overflow, true, true).Outputs!;
        Assert.True(double.IsNaN(committed["Signal1"]));
        Assert.Equal(0, committed["Signal2"]);
        Assert.True(double.IsNaN(state.Update(stable, true, true).Outputs!["Signal1"]));
        state.Reset();
        Assert.Equal(0, state.Update(stable, true, true).Outputs!["Signal1"]);
    }
}

using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class AwesomeOscillatorNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(AwesomeOscillator) || c.IndicatorType == typeof(AcceleratorOscillator))
        .Select(c => new object[] { c });

    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High),
        bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("AWESOME", BarTimeframe.Minutes(1), b.Time, b.Time,
        b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Theory, MemberData(nameof(Cases))]
    public void EveryRouteMatchesIndependentMedianAndAverageStages(IndicatorValidationCase testCase)
    {
        var builtIn = (IBuiltInIndicator)testCase.Factory();
        var options = builtIn.CreateOptions();
        var accelerator = builtIn.BatchName == IndicatorName.AcceleratorOscillator;
        var spec = new IndicatorSpec(builtIn.BatchName, options);
        Assert.True(BuilderArmBinding.TryGetTarget(options.GetType(), out var target));
        foreach (var fixture in IndicatorAdversarialCases.Generate(80, 244))
        {
            var bars = fixture.Bars;
            var expected = BuiltInFormulaReferences.RoundedAwesomeReference(bars, options, accelerator);
            Assert.Equal(expected, BuilderArmBinding.Compute(Data(bars), spec, target));
            using var context = new ComputeContext();
            using var buffer = IndicatorCompute.TryComputeFast(Data(bars), spec, context);
            Assert.NotNull(buffer);
            Assert.Equal(expected, buffer.Value.ToArray());
            foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
            {
                Assert.NotNull(state);
                using var lifetime = state as IDisposable;
                for (var replay = 0; replay < 2; replay++)
                {
                    state.Reset();
                    for (var i = 0; i < bars.Count; i++)
                    foreach (var commit in new[] { false, true })
                        Assert.Equal(expected[i], state.Update(Native(bars[i]), commit, true).Value);
                }
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SelectedInputReplacesMedianForPublicAndFastRoutes(bool accelerator)
    {
        var bars = IndicatorAdversarialCases.Generate(80, 244).Single(f => f.Name.EndsWith("/negative")).Bars;
        var source = new Sma(2);
        IIndicator indicator = accelerator ? new AcceleratorOscillator(2).Of(source) : new AwesomeOscillator(2).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        var selected = run[source].ToArray();
        var builtIn = (IBuiltInIndicator)indicator;
        var expected = BuiltInFormulaReferences.RoundedAwesomeReference(bars, builtIn.CreateOptions(), accelerator, selected);
        Assert.Equal(expected, run[indicator].ToArray());
        var data = Data(bars);
        data.CustomValuesList = selected.ToList();
        using var context = new ComputeContext();
        using var buffer = IndicatorCompute.TryComputeFast(data, new IndicatorSpec(builtIn.BatchName, builtIn.CreateOptions()), context);
        Assert.NotNull(buffer);
        Assert.Equal(expected, buffer.Value.ToArray());
    }

    [Fact]
    public void CoreUsesFiniteMedianAndFullWindowMeans()
    {
        Core.OscillatorCore.AwesomeOscillator([], [], [], int.MaxValue, int.MaxValue);
        Core.OscillatorCore.AcceleratorOscillator([], [], [], int.MaxValue, int.MaxValue, int.MaxValue);
        foreach (var fixture in IndicatorAdversarialCases.Generate(80, 244))
        {
            var bars = fixture.Bars;
            var high = bars.Select(b => b.High).ToArray();
            var low = bars.Select(b => b.Low).ToArray();
            var actual = new double[bars.Count];
            Core.OscillatorCore.AwesomeOscillator(high, low, actual, 2);
            Assert.Equal(BuiltInFormulaReferences.RoundedAwesomeReference(bars, new AwesomeOscillatorSpecOptions(2), false), actual);
            Core.OscillatorCore.AcceleratorOscillator(high, low, actual, 2);
            Assert.Equal(BuiltInFormulaReferences.RoundedAwesomeReference(bars, new AcceleratorOscillatorSpecOptions(2), true), actual);
        }
        var simple = new[] { 1d, 2, 4 }.Select(v => new Bar(DateTime.UnixEpoch, v, v, v, v, 1)).ToArray();
        Assert.Equal(new[] { 1d, .5, 1 }, Data(simple).CalculateAwesomeOscillator(fastLength: 1, slowLength: 2).CustomValuesList);
        Assert.Equal(new[] { 1d, -.25, .25 }, Data(simple).CalculateAcceleratorOscillator(fastLength: 1, slowLength: 2, smoothLength: 2).CustomValuesList);
    }

    [Fact]
    public void OverflowPreviewDoesNotCommitInvalidAcceleration()
    {
        OhlcvBar B(double value) => Native(new Bar(DateTime.UnixEpoch, value, value, value, value, 1));
        using var state = new AcceleratorOscillatorState(1, 5, 2);
        for (var i = 0; i < 5; i++) state.Update(B(-double.MaxValue), true, true);
        Assert.True(double.IsNaN(state.Update(B(double.MaxValue), false, true).Value));
        Assert.Equal(0, state.Update(B(-double.MaxValue), false, true).Value);
        Assert.True(double.IsNaN(state.Update(B(double.MaxValue), true, true).Value));
        Assert.True(double.IsNaN(state.Update(B(-double.MaxValue), true, true).Value));
        state.Reset();
        Assert.Equal(1, state.Update(B(1), true, true).Value);
    }
}

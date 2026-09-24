using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class MidpointNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(MidpointOscillator)).Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Theory, MemberData(nameof(Cases))]
    public void EveryRouteMatchesIndependentMidpointFormula(IndicatorValidationCase testCase)
    {
        var builtIn = (IBuiltInIndicator)testCase.Factory();
        var options = builtIn.CreateOptions();
        var spec = new IndicatorSpec(builtIn.BatchName, options);
        Assert.True(BuilderArmBinding.TryGetTarget(options.GetType(), out var target));
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        {
            var midpoint = (MidpointOscillatorSpecOptions)options;
            var expected = BuiltInFormulaReferences.RoundedMidpoint(fixture.Bars, midpoint.Length, midpoint.MaType == MovingAvgType.WeightedMovingAverage ? 2 : 3);
            var count = fixture.Bars.Count;
            var bars = fixture.Bars.Take(count).ToArray();
            StockData Data() => new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            foreach (var key in expected.Keys)
            {
                var outputSpec = new IndicatorSpec(builtIn.BatchName, options, key);
                Assert.Equal(expected[key].Take(count), BuilderArmBinding.Compute(Data(), outputSpec, target));
                using var context = new ComputeContext();
                using var buffer = IndicatorCompute.ComputeArm(Data(), outputSpec, context);
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
                        var native = new OhlcvBar("GUPPY", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
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

    [Fact]
    public void ExactPositionPreservesCenterEndpointsClampsAndSubnormalRanges()
    {
        foreach (var magnitude in new[] { double.Epsilon, 1d, double.MaxValue })
        {
            Assert.Equal(0, RoundedMidpointOscillator.Of(0, magnitude, -magnitude));
            Assert.Equal(100, RoundedMidpointOscillator.Of(magnitude, magnitude, -magnitude));
            Assert.Equal(-100, RoundedMidpointOscillator.Of(-magnitude, magnitude, -magnitude));
            Assert.Equal(0, RoundedMidpointOscillator.Of(magnitude, magnitude, magnitude));
        }
        Assert.Equal(100, RoundedMidpointOscillator.Of(double.MaxValue, 1, 0));
        Assert.Equal(-100, RoundedMidpointOscillator.Of(-double.MaxValue, 1, 0));
        Assert.Equal(100d / 3, RoundedMidpointOscillator.Of(2 * double.Epsilon, 3 * double.Epsilon, 0));
    }

    [Fact]
    public async Task SelectedInputReachesBuilderAndLegacyRoutes()
    {
        var bars = Enumerable.Range(0, 40).Select(i =>
            new Bar(DateTime.UnixEpoch.AddDays(i), 10, 20, 1, 5 + i % 7, 1)).ToArray();
        var source = new Sma(2);
        var indicator = new MidpointOscillator(3).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        var selected = run[source].ToArray();
        // Typed Of() replaces close while retaining the original high/low.
        var typedBars = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray();
        Assert.Equal(BuiltInFormulaReferences.RoundedMidpoint(typedBars, 3, 3)["Mo"], run[indicator.Outputs[0]].ToArray());
        // Legacy selected-input lists retain the documented custom-range projection.
        var projected = bars.Select((b, i) => new Bar(b.Time, selected[i],
            selected[i] >= 1 ? b.High : Math.Max(selected[Math.Max(0, i - 1)], selected[i]),
            selected[i] >= 1 ? b.Low : Math.Min(selected[Math.Max(0, i - 1)], selected[i]), selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedMidpoint(projected, 3, 3)["Mo"];
        foreach (var chained in new[] { false, true })
        {
            var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            if (chained) data.CustomValuesList = selected.ToList(); else data.InputValues = selected.ToList();
            using var context = new ComputeContext();
            using var buffer = IndicatorCompute.ComputeMidpointOscillatorFast(data, context, 3);
            Assert.Equal(expected, buffer.ToArray());
            Assert.Equal(expected, data.CalculateMidpointOscillator(length: 3).OutputValues["Mo"]);
        }
    }
}

using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class DisplacedEnvelopeNumericalTests
{
    [Theory]
    [InlineData(-100)]
    [InlineData(0)]
    [InlineData(1e-14)]
    [InlineData(100)]
    [InlineData(double.MaxValue)]
    public void PercentagesPreserveDelayedCenterDespiteOuterOverflow(double percent)
    {
        var prices = new[] { double.MaxValue, -double.MaxValue, double.Epsilon, 1, 2, 3 };
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 1)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedDisplacedEnvelope(bars, new MovingAverageDisplacedEnvelopeSpecOptions(1, 2, percent));
        var data = new StockData(prices, prices, prices, prices, prices.Select(_ => 1d), bars.Select(b => b.Time));
        var legacy = data.CalculateMovingAverageDisplacedEnvelope(length1: 1, length2: 2, pct: percent);
        using var state = new MovingAverageDisplacedEnvelopeState(length1: 1, length2: 2, pct: percent);
        for (var i = 0; i < bars.Length; i++)
        {
            var b = bars[i];
            var native = new OhlcvBar("ENVELOPE", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
            foreach (var commit in new[] { false, true })
            {
                var actual = state.Update(native, commit, true).Outputs!;
                foreach (var key in expected.Keys)
                {
                    Assert.Equal(expected[key][i], legacy.OutputValues[key][i]);
                    Assert.Equal(expected[key][i], actual[key]);
                }
            }
        }
        Assert.Equal(0, legacy.OutputValues["MiddleBand"][1]);
        Assert.Equal(double.MaxValue, legacy.OutputValues["MiddleBand"][2]);
        Assert.Equal(double.Epsilon, legacy.OutputValues["MiddleBand"][4]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void LegacyAndNativeNormalizeNonpositiveDelay(int delay)
    {
        var prices = new[] { 1d, 2, 4 };
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 1)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedDisplacedEnvelope(bars, new MovingAverageDisplacedEnvelopeSpecOptions(2, delay));
        var data = new StockData(prices, prices, prices, prices, prices.Select(_ => 1d), bars.Select(b => b.Time));
        var legacy = data.CalculateMovingAverageDisplacedEnvelope(length1: 2, length2: delay);
        foreach (var key in expected.Keys) Assert.Equal(expected[key], legacy.OutputValues[key]);
        using var state = new MovingAverageDisplacedEnvelopeState(length1: 2, length2: delay);
        for (var i = 0; i < bars.Length; i++)
        {
            var b = bars[i];
            var native = new OhlcvBar("DELAY", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
            var actual = state.Update(native, true, true).Outputs!;
            foreach (var key in expected.Keys) Assert.Equal(expected[key][i], actual[key]);
        }
    }

    [Fact]
    public async Task SelectedInputControlsDelayedAverage()
    {
        var bars = Enumerable.Range(0, 40).Select(i =>
            new Bar(DateTime.UnixEpoch.AddDays(i), 10, 20, 1, 5 + i % 7, 1)).ToArray();
        var source = new Sma(2);
        var indicator = new MovingAverageDisplacedEnvelope(3, 2).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedDisplacedEnvelope(projected, new MovingAverageDisplacedEnvelopeSpecOptions(3, 2));
        var keys = new[] { "UpperBand", "MiddleBand", "LowerBand" };
        for (var slot = 0; slot < keys.Length; slot++) Assert.Equal(expected[keys[slot]], run[indicator.Outputs[slot]].ToArray());
    }

    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(MovingAverageDisplacedEnvelope)).Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Theory, MemberData(nameof(Cases))]
    public void EveryRouteMatchesIndependentDisplacedEnvelopeFormula(IndicatorValidationCase testCase)
    {
        var builtIn = (IBuiltInIndicator)testCase.Factory();
        var options = builtIn.CreateOptions();
        var spec = new IndicatorSpec(builtIn.BatchName, options);
        Assert.True(BuilderArmBinding.TryGetTarget(options.GetType(), out var target));
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        {
            var expected = BuiltInFormulaReferences.RoundedDisplacedEnvelope(fixture.Bars, options);
            var count = fixture.Bars.Count;
            var bars = fixture.Bars.Take(count).ToArray();
            StockData Data() => new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            foreach (var key in expected.Keys)
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

}

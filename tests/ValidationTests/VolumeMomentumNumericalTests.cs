using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class VolumeMomentumOscillatorNumericalTests
{
    [Fact]
    public void ExtremeRatioAndFirstBarSeedHaveIndependentExpectedValues()
    {
        var prices = new[] { -double.MaxValue, double.MaxValue, 3, 3 };
        var actual = new double[prices.Length];
        Core.OscillatorCore.VolumeMomentumOscillator(prices, actual, 1, 5);
        Assert.Equal(0, actual[0]);
        Assert.InRange(actual[1], -401, -399);
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), 1, 1, 1, 1, v)).ToArray();
        Assert.Equal(BuiltInFormulaReferences.RoundedVolumeMomentumOscillator(bars, new VolumeMomentumOscillatorSpecOptions(1, 5)), actual);
        Core.OscillatorCore.VolumeMomentumOscillator(new[] { 2d, 4 }, actual, 1, 3);
        Assert.Equal((new ReferenceFraction(100) / new ReferenceFraction(3)).ToDouble(), actual[1]);
    }

    [Fact]
    public async Task SelectedInputControlsTheBuilder()
    {
        var bars = IndicatorAdversarialCases.Generate(40, 244).Single(f => f.Name.EndsWith("/negative")).Bars;
        var source = new Sma(2);
        var indicator = new VolumeMomentumOscillator(2, 4).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, b.Close, selected[i])).ToArray();
        Assert.Equal(BuiltInFormulaReferences.RoundedVolumeMomentumOscillator(projected, ((IBuiltInIndicator)indicator).CreateOptions()), run[indicator].ToArray());
        var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
            bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
        data.CustomValuesList = selected.ToList();
        var legacy = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
            bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
        legacy.CustomValuesList = selected.ToList();
        Assert.Equal(BuiltInFormulaReferences.RoundedVolumeMomentumOscillator(projected, ((IBuiltInIndicator)indicator).CreateOptions()),
            legacy.CalculateVolumeMomentumOscillator(shortLength: 2, longLength: 4).OutputValues["Vmo"]);
        using var context = new ComputeContext();
        using var buffer = IndicatorCompute.TryComputeFast(data, new IndicatorSpec(Enums.IndicatorName.VolumeMomentumOscillator, ((IBuiltInIndicator)indicator).CreateOptions()), context);
        Assert.NotNull(buffer);
        Assert.Equal(BuiltInFormulaReferences.RoundedVolumeMomentumOscillator(projected, ((IBuiltInIndicator)indicator).CreateOptions()), buffer.Value.ToArray());
    }

    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(VolumeMomentumOscillator)).Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Theory, MemberData(nameof(Cases))]
    public void EveryRouteMatchesIndependentSeededStages(IndicatorValidationCase testCase)
    {
        var builtIn = (IBuiltInIndicator)testCase.Factory();
        var options = builtIn.CreateOptions();
        var spec = new IndicatorSpec(builtIn.BatchName, options);
        Assert.True(BuilderArmBinding.TryGetTarget(options.GetType(), out var target));
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        {
            var expected = new Dictionary<string, double[]> { ["Vmo"] = BuiltInFormulaReferences.RoundedVolumeMomentumOscillator(fixture.Bars, options) };
            var count = fixture.Bars.Count;
            for (var i = 0; i < count; i++)
                if (expected.Values.Any(values => !double.IsFinite(values[i]))) { count = i; break; }
            var bars = fixture.Bars.Take(count).ToArray();
            StockData Data() => new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            var publishedKeys = testCase.Factory().Outputs.Count == 1 ? new[] { builtIn.BatchOutputKey ?? "Vmo" } : expected.Keys.ToArray();
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

using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class MadBandNumericalTests
{
    [Fact]
    public void DeviationPreservesScaleReflectionPartialStartupAndExpiry()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, Math.Pow(2, 300) })
        foreach (var sign in new[] { -1, 1 })
        {
            using var window = new ExactMeanAbsoluteDeviationWindow(3);
            Assert.Equal(0, window.Next(sign * scale, true));
            Assert.Equal(0, window.Next(sign * scale, true));
            var expected = (ReferenceFraction.FromDouble(scale) * new ReferenceFraction(4) / new ReferenceFraction(3)).ToDouble();
            Assert.Equal(expected, window.Next(sign * 4 * scale, false));
            Assert.Equal(expected, window.Next(sign * 4 * scale, true));
            window.Next(sign * 4 * scale, true);
            Assert.Equal(0, window.Next(sign * 4 * scale, true));
            window.Reset();
            Assert.Equal(0, window.Next(sign * scale, true));
        }
        using var extremes = new ExactMeanAbsoluteDeviationWindow(2);
        extremes.Next(double.MaxValue, true);
        Assert.Equal(double.MaxValue, extremes.Next(-double.MaxValue, false));
        Assert.Equal(0, extremes.Next(double.MaxValue, true));
        Assert.Equal(double.MaxValue, extremes.Next(-double.MaxValue, true));
        Assert.Equal(0, extremes.Next(-double.MaxValue, true));
    }

    [Theory]
    [InlineData(-2)]
    [InlineData(0)]
    [InlineData(0.5)]
    [InlineData(double.MaxValue)]
    public void MultiplierAndPartialDeviationMatchIndependentBands(double multiplier)
    {
        var prices = new[] { -double.MaxValue, double.MaxValue, double.MaxValue, 1, 2, 3 };
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 1)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedMadBands(bars, new MeanAbsoluteDeviationBandsSpecOptions(2, multiplier));
        var data = new StockData(prices, prices, prices, prices, prices.Select(_ => 1d), bars.Select(b => b.Time));
        var legacy = data.CalculateMeanAbsoluteDeviationBands(multiplier, length: 2);
        using var state = new MeanAbsoluteDeviationBandsState(multiplier, length: 2);
        for (var i = 0; i < bars.Length; i++)
        {
            var b = bars[i];
            var native = new OhlcvBar("MAD", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
            var actual = state.Update(native, true, true).Outputs!;
            foreach (var key in expected.Keys)
            {
                Assert.Equal(expected[key][i], legacy.OutputValues[key][i]);
                Assert.Equal(expected[key][i], actual[key]);
            }
        }
    }

    [Fact]
    public async Task SelectedInputControlsCenterAndDeviation()
    {
        var bars = Enumerable.Range(0, 40).Select(i =>
            new Bar(DateTime.UnixEpoch.AddDays(i), 10, 20, 1, 5 + i % 7, 1)).ToArray();
        var source = new Sma(2);
        var indicator = new MeanAbsoluteDeviationBands(3).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedMadBands(projected, new MeanAbsoluteDeviationBandsSpecOptions(3));
        var keys = new[] { "UpperBand", "MiddleBand", "LowerBand" };
        for (var slot = 0; slot < keys.Length; slot++) Assert.Equal(expected[keys[slot]], run[indicator.Outputs[slot]].ToArray());
    }

    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(MeanAbsoluteDeviationBands)).Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Theory, MemberData(nameof(Cases))]
    public void EveryRouteMatchesIndependentMadBandFormula(IndicatorValidationCase testCase)
    {
        var builtIn = (IBuiltInIndicator)testCase.Factory();
        var options = builtIn.CreateOptions();
        var spec = new IndicatorSpec(builtIn.BatchName, options);
        Assert.True(BuilderArmBinding.TryGetTarget(options.GetType(), out var target));
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        {
            var expected = BuiltInFormulaReferences.RoundedMadBands(fixture.Bars, options);
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

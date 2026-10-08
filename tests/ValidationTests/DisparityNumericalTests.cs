using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class DisparityNumericalTests
{
    [Fact]
    public void RoundedMeanDefinesWarmupZeroDenominatorsAndFiniteExtremeRatios()
    {
        foreach (var prices in new[] {
            new[] { 1d, 2, 4 },
            new[] { -double.MaxValue, -double.MaxValue, double.MaxValue },
            new[] { double.Epsilon, 0d, double.Epsilon },
            new[] { double.MaxValue, -double.MaxValue, 0d } })
        {
            var bars = prices.Select(v => new Bar(DateTime.UnixEpoch, v, v, v, v, 1)).ToArray();
            foreach (var length in new[] { 2, 3 })
            {
                var expected = BuiltInFormulaReferences.RoundedDisparity(bars, new DisparityIndexSpecOptions(length));
                var actual = new double[prices.Length];
                OscillatorCore.DisparityIndex(prices, actual, length);
                Assert.Equal(expected, actual);
                Assert.All(actual, value => Assert.True(double.IsFinite(value)));
            }
        }
        var output = new double[3];
        OscillatorCore.DisparityIndex(new[] { 1d, 2, 4 }, output, 2);
        Assert.Equal(new[] { 0d, 100d / 3, 100d / 3 }, output);
        OscillatorCore.DisparityIndex(new[] { double.MaxValue, -double.MaxValue, 0d }, output, 3);
        Assert.Equal(new double[3], output);
    }

    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(DisparityIndex))
        .Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Theory, MemberData(nameof(Cases))]
    public void EveryRouteMatchesIndependentRoundedMeanRatio(IndicatorValidationCase testCase)
    {
        var indicator = testCase.Factory();
        var builtIn = (IBuiltInIndicator)indicator;
        var options = builtIn.CreateOptions();
        var length = (int)options.GetType().GetProperty("Length")!.GetValue(options)!;
        var key = "Di";
        var spec = new IndicatorSpec(builtIn.BatchName, options);
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        {
            var bars = fixture.Bars;
            var expected = BuiltInFormulaReferences.RoundedDisparity(bars, options);
            var prices = bars.Select(b => b.Close).ToArray();
            StockData Data() => new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                prices, bars.Select(b => b.Volume), bars.Select(b => b.Time));
            var legacy = Data().CalculateDisparityIndex(((DisparityIndexSpecOptions)options).MaType, length);
            Assert.Equal(expected, legacy.OutputValues[key]);
            Assert.True(BuilderArmBinding.TryGetTarget(options.GetType(), out var target));
            Assert.Equal(expected, BuilderArmBinding.Compute(Data(), spec, target));
            using (var context = new ComputeContext())
            {
                using var buffer = IndicatorCompute.TryComputeFast(Data(), spec, context);
                Assert.NotNull(buffer);
                Assert.Equal(expected, buffer.Value.ToArray());
            }
            foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
            {
                Assert.NotNull(state);
                using var lifetime = state as IDisposable;
                for (var replay = 0; replay < 2; replay++)
                {
                    state.Reset();
                    for (var i = 0; i < bars.Count; i++)
                    {
                        var b = bars[i];
                        var native = new OhlcvBar("ERROR", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                        foreach (var commit in new[] { false, true })
                            Assert.Equal(expected[i], state.Update(native, commit, true).Outputs![key]);
                    }
                }
            }
        }
    }

    [Fact]
    public async Task SelectedInputReachesBuilderAndLegacyRoutes()
    {
        var bars = IndicatorAdversarialCases.Generate(40, 244).Single(f => f.Name.EndsWith("/negative")).Bars;
        var source = new Sma(2);
        var indicator = new DisparityIndex(3).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, selected[i], selected[i], selected[i], selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedDisparity(projected, new DisparityIndexSpecOptions(3));
        Assert.Equal(expected, run[indicator.Outputs[0]].ToArray());
        foreach (var chained in new[] { false, true })
        {
            var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            if (chained) data.CustomValuesList = selected.ToList(); else data.InputValues = selected.ToList();
            using var context = new ComputeContext();
            using var buffer = IndicatorCompute.ComputeDisparityIndexFast(data, context, 3);
            Assert.Equal(expected, buffer.ToArray());
            Assert.Equal(expected, data.CalculateDisparityIndex(length: 3).OutputValues["Di"]);
        }
    }
}

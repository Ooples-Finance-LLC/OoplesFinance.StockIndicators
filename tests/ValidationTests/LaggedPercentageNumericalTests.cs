using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class LaggedPercentageNumericalTests
{
    [Theory]
    [InlineData(-2)]
    [InlineData(0)]
    [InlineData(1)]
    public void EveryCorePreservesFiniteExtremeChangesAndNormalizesPeriods(int length)
    {
        var prices = new[] { double.MaxValue, -double.MaxValue, double.Epsilon, 0, 7, 7 };
        var expected = new[] { 0d, -200, -100, -100, 0, 0 };
        var actual = new double[prices.Length];
        TrendCore.PercentageChange(prices, actual, length);
        Assert.Equal(expected, actual);
        OscillatorCore.PercentChange(prices, actual, length);
        Assert.Equal(expected, actual);
        OscillatorCore.PerformanceIndex(prices, actual, length);
        Assert.Equal(expected, actual);
        var data = new StockData(prices, prices, prices, prices, prices.Select(_ => 1d), prices.Select((_, i) => DateTime.UnixEpoch.AddDays(i)));
        Assert.Equal(expected, data.CalculatePerformanceIndex(length).OutputValues["PerformanceIndex"]);
    }

    [Fact]
    public void PercentageContractPreservesLagScaleAndReflection()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, Math.Pow(2, 500) })
        foreach (var sign in new[] { -1, 1 })
        {
            var prices = new[] { sign * scale, sign * 2 * scale, sign * 4 * scale };
            var output = new double[3];
            OscillatorCore.PerformanceIndex(prices, output, 2);
            Assert.Equal(new[] { 0d, 0, 300 }, output);
        }
    }

    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(PerformanceIndex) || c.IndicatorType == typeof(PercentageChange) || c.IndicatorType == typeof(PercentChange))
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
    public void EveryAliasAndRouteMatchesIndependentPercentageFormula(IndicatorValidationCase testCase)
    {
        var indicator = testCase.Factory();
        var builtIn = (IBuiltInIndicator)indicator;
        var options = builtIn.CreateOptions();
        var length = (int)options.GetType().GetProperty("Length")!.GetValue(options)!;
        var key = "PerformanceIndex";
        var spec = new IndicatorSpec(builtIn.BatchName, options);
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        {
            var bars = fixture.Bars;
            var expected = BuiltInFormulaReferences.RoundedLaggedPercentage(bars, length);
            var prices = bars.Select(b => b.Close).ToArray();
            StockData Data() => new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                prices, bars.Select(b => b.Volume), bars.Select(b => b.Time));
            var legacy = Data().CalculatePerformanceIndex(length);
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

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task SelectedInputReachesEveryAliasAndLegacyRoute(int alias)
    {
        var bars = IndicatorAdversarialCases.Generate(40, 244).Single(f => f.Name.EndsWith("/negative")).Bars;
        var source = new Sma(2);
        IIndicator indicator = alias == 0 ? new PerformanceIndex(3).Of(source)
            : alias == 1 ? new PercentageChange(3).Of(source) : new PercentChange(3).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, selected[i], selected[i], selected[i], selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedLaggedPercentage(projected, 3);
        Assert.Equal(expected, run[indicator.Outputs[0]].ToArray());
        foreach (var chained in new[] { false, true })
        {
            var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            if (chained) data.CustomValuesList = selected.ToList(); else data.InputValues = selected.ToList();
            using var context = new ComputeContext();
            var builtIn = (IBuiltInIndicator)indicator;
            var spec = new IndicatorSpec(builtIn.BatchName, builtIn.CreateOptions());
            using var buffer = IndicatorCompute.TryComputeFast(data, spec, context);
            Assert.NotNull(buffer);
            Assert.Equal(expected, buffer.Value.ToArray());
            Assert.Equal(expected, data.CalculatePerformanceIndex(3).OutputValues["PerformanceIndex"]);
        }
    }
}

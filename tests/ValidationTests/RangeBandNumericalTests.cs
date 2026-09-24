using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class RangeBandNumericalTests
{
    [Theory]
    [InlineData(-2)]
    [InlineData(0)]
    [InlineData(0.5)]
    [InlineData(double.MaxValue)]
    public void MultipliersPreserveFiniteCancellableBands(double multiplier)
    {
        var prices = new[] { double.MaxValue, -double.MaxValue, double.MaxValue, double.Epsilon, 0, 1, 2 };
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 1)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedRangeBands(bars, new RangeBandsSpecOptions(2, multiplier));
        var data = new StockData(prices, prices, prices, prices, prices.Select(_ => 1d), bars.Select(b => b.Time));
        var legacy = data.CalculateRangeBands(multiplier, length: 2);
        using var state = new RangeBandsState(multiplier, length: 2);
        for (var i = 0; i < bars.Length; i++)
        {
            var b = bars[i];
            var native = new OhlcvBar("RANGE", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
            var actual = state.Update(native, true, true).Outputs!;
            foreach (var key in expected.Keys)
            {
                Assert.Equal(expected[key][i], legacy.OutputValues[key][i]);
                Assert.Equal(expected[key][i], actual[key]);
            }
        }
        Assert.Equal(double.MaxValue, RangeBandArithmetic.Band(-double.MaxValue, double.MaxValue, -double.MaxValue, 1));
        Assert.Equal(0, RangeBandArithmetic.Band(0, double.MaxValue, -double.MaxValue, 0));
        Assert.Equal(double.Epsilon, RangeBandArithmetic.Band(0, double.Epsilon, -double.Epsilon, 0.5));
    }

    [Fact]
    public void QuartileMidpointSurvivesOverflowingOuterBands()
    {
        var prices = new[] { -double.MaxValue, double.MaxValue, double.MaxValue, 1, 2, 3 };
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 1)).ToArray();
        foreach (var multiplier in new[] { -2d, 0, 0.5, double.MaxValue })
        {
            var expected = BuiltInFormulaReferences.RoundedQuartileBands(bars, 2, multiplier);
            var data = new StockData(prices, prices, prices, prices, prices.Select(_ => 1d), bars.Select(b => b.Time));
            var legacy = data.CalculateInterquartileRangeBands(2, multiplier);
            using var state = new InterquartileRangeBandsState(2, multiplier);
            for (var i = 0; i < bars.Length; i++)
            {
                var b = bars[i];
                var native = new OhlcvBar("IQR", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                var actual = state.Update(native, true, true).Outputs!;
                foreach (var key in expected.Keys)
                {
                    Assert.Equal(expected[key][i], legacy.OutputValues[key][i]);
                    Assert.Equal(expected[key][i], actual[key]);
                }
            }
            Assert.Equal(0, legacy.OutputValues["MiddleBand"][1]);
            Assert.Equal(double.MaxValue, legacy.OutputValues["MiddleBand"][2]);
        }
        Assert.Equal(double.Epsilon, RangeBandArithmetic.Midpoint(double.Epsilon, double.Epsilon));
    }

    [Fact]
    public async Task SelectedInputSurvivesBuilderFallbackCloning()
    {
        var bars = Enumerable.Range(0, 40).Select(i =>
            new Bar(DateTime.UnixEpoch.AddDays(i), 10, 20, 1, 5 + i % 7, 1)).ToArray();
        var source = new Sma(2);
        var indicator = new RangeBands(3).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray();
        var options = new RangeBandsSpecOptions(3);
        var expected = BuiltInFormulaReferences.RoundedRangeBands(projected, options);
        var keys = new[] { "UpperBand", "MiddleBand", "LowerBand" };
        for (var slot = 0; slot < keys.Length; slot++) Assert.Equal(expected[keys[slot]], run[indicator.Outputs[slot]].ToArray());
        foreach (var chained in new[] { false, true })
        foreach (var key in keys)
        {
            var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            if (chained) data.CustomValuesList = selected.ToList(); else data.InputValues = selected.ToList();
            using var context = new ComputeContext();
            using var buffer = IndicatorCompute.TryComputeFast(data, new IndicatorSpec(IndicatorName.RangeBands, options, key), context);
            Assert.NotNull(buffer);
            Assert.Equal(expected[key], buffer.Value.ToArray());
            Assert.Equal(selected, chained ? data.CustomValuesList : data.InputValues);
            Assert.Equal(expected[key], data.CalculateRangeBands(length: 3).OutputValues[key]);
        }
    }

    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(RangeBands)).Select(c => new object[] { c });

    public static IEnumerable<object[]> QuartileCases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(InterquartileRangeBands)).Select(c => new object[] { c });

    [Theory, MemberData(nameof(QuartileCases))]
    public Task EveryQuartileConfigurationReceivesEveryNumericalClass(IndicatorValidationCase testCase)
        => EveryConfigurationReceivesEveryNumericalClass(testCase);

    [Theory, MemberData(nameof(QuartileCases))]
    public void EveryQuartileRouteMatchesIndependentFormula(IndicatorValidationCase testCase)
        => EveryRouteMatchesIndependentRangeBandFormula(testCase);

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Theory, MemberData(nameof(Cases))]
    public void EveryRouteMatchesIndependentRangeBandFormula(IndicatorValidationCase testCase)
    {
        var builtIn = (IBuiltInIndicator)testCase.Factory();
        var options = builtIn.CreateOptions();
        var spec = new IndicatorSpec(builtIn.BatchName, options);
        Assert.True(BuilderArmBinding.TryGetTarget(options.GetType(), out var target));
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        {
            var expected = options is InterquartileRangeBandsSpecOptions quartile
                ? BuiltInFormulaReferences.RoundedQuartileBands(fixture.Bars, quartile.Length, quartile.Mult)
                : BuiltInFormulaReferences.RoundedRangeBands(fixture.Bars, options);
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

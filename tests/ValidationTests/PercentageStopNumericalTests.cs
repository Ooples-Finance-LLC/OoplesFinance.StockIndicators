using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class PercentageStopNumericalTests
{
    [Fact]
    public void PriorWindowExpiryAndStrictBreakoutsControlBothStops()
    {
        var highs = new[] { 10d, 9, 8, 9.5 };
        var lows = new[] { 2d, 3, 4, 2.5 };
        var close = Enumerable.Repeat(6d, highs.Length).ToArray();
        var dates = Enumerable.Range(0, highs.Length).Select(i => DateTime.UnixEpoch.AddDays(i)).ToArray();
        var data = new StockData(close, highs, lows, close, close.Select(_ => 1d), dates);
        var actual = data.CalculatePercentageTrailingStops(2, 10);
        Assert.Equal(new[] { 9d, 9, 9, 8.55 }, actual.OutputValues["LongStop"]);
        Assert.Equal(new[] { 2.2, 2.2, 2.2, 2.75 }, actual.OutputValues["ShortStop"]);
        using var flat = new PercentageTrailingStopsState(2, 10);
        for (var i = 0; i < 4; i++)
        {
            var bar = new OhlcvBar("STOPS", BarTimeframe.Minutes(1), dates[i], dates[i], 7, 7, 7, 7, 1, true);
            var outputs = flat.Update(bar, true, true).Outputs!;
            Assert.Equal(7, outputs["LongStop"]);
            Assert.Equal(7, outputs["ShortStop"]);
        }
    }

    [Theory]
    [InlineData(-100)]
    [InlineData(0)]
    [InlineData(1e-14)]
    [InlineData(100)]
    [InlineData(double.MaxValue)]
    public void PercentageArithmeticRetainsFiniteExtremaAndDeclaresRealOverflow(double percent)
    {
        var bars = new[] {
            new Bar(DateTime.UnixEpoch, 0, double.MaxValue, -double.MaxValue, 0, 1),
            new Bar(DateTime.UnixEpoch.AddDays(1), 0, double.Epsilon, -double.Epsilon, 0, 1),
            new Bar(DateTime.UnixEpoch.AddDays(2), 0, 1, -1, 0, 1) };
        var expected = BuiltInFormulaReferences.RoundedPercentageStops(bars, new PercentageTrailingStopsSpecOptions(1, percent));
        var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
            bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
        var actual = data.CalculatePercentageTrailingStops(1, percent);
        foreach (var key in expected.Keys) Assert.Equal(expected[key], actual.OutputValues[key]);
        using var state = new PercentageTrailingStopsState(1, percent);
        for (var i = 0; i < bars.Length; i++)
        {
            var b = bars[i];
            var bar = new OhlcvBar("STOPS", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
            foreach (var commit in new[] { false, true })
            {
                var outputs = state.Update(bar, commit, true).Outputs!;
                foreach (var key in expected.Keys) Assert.Equal(expected[key][i], outputs[key]);
            }
        }
    }

    [Fact]
    public async Task SelectedInputControlsInitialCloseWhileRetainingBarRanges()
    {
        var bars = Enumerable.Range(0, 40).Select(i => new Bar(DateTime.UnixEpoch.AddDays(i), 10, 20, 1, 5 + i % 7, 1)).ToArray();
        var source = new Sma(2);
        var indicator = new PercentageTrailingStops(3, 10).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedPercentageStops(projected, new PercentageTrailingStopsSpecOptions(3, 10));
        Assert.Equal(expected["LongStop"], run[indicator.Outputs[0]].ToArray());
        Assert.Equal(expected["ShortStop"], run[indicator.Outputs[1]].ToArray());
        Assert.Equal(0, run[indicator.Outputs[1]].ToArray()[0]);
    }

    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(PercentageTrailingStops)).Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Theory, MemberData(nameof(Cases))]
    public void EveryRouteMatchesIndependentBreakoutFormula(IndicatorValidationCase testCase)
    {
        var builtIn = (IBuiltInIndicator)testCase.Factory();
        var options = builtIn.CreateOptions();
        var spec = new IndicatorSpec(builtIn.BatchName, options);
        Assert.True(BuilderArmBinding.TryGetTarget(options.GetType(), out var target));
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        {
            var expected = BuiltInFormulaReferences.RoundedPercentageStops(fixture.Bars, options);
            var count = fixture.Bars.Count;
            var bars = fixture.Bars.Take(count).ToArray();
            StockData Data() => new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            foreach (var key in expected.Keys)
            {
                var outputSpec = new IndicatorSpec(builtIn.BatchName, options, key);
                Assert.Equal(expected[key].Take(count), BuilderArmBinding.Compute(Data(), outputSpec, target));
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

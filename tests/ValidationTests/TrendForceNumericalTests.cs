using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class TrendForceNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(TrendForceHistogram)).Select(c => new object[] { c });
    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High),
        bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("TREND", BarTimeframe.Minutes(1), b.Time, b.Time,
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
    public void BatchAndStreamingMatchIndependentEventCounts(IndicatorValidationCase testCase)
    {
        var builtIn = (IBuiltInIndicator)testCase.Factory();
        var options = (TrendForceHistogramSpecOptions)builtIn.CreateOptions();
        var spec = new IndicatorSpec(builtIn.BatchName, options);
        Assert.True(BuilderArmBinding.TryGetTarget(options.GetType(), out var target));
        foreach (var fixture in IndicatorAdversarialCases.Generate(80, 244))
        {
            var bars = fixture.Bars;
            var expected = BuiltInFormulaReferences.RoundedTrendForceReference(bars, options.Length);
            Assert.Equal(expected, BuilderArmBinding.Compute(Data(bars), spec, target));
            foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
            {
                Assert.NotNull(state);
                using var lifetime = state as IDisposable;
                for (var replay = 0; replay < 2; replay++)
                {
                    state.Reset();
                    for (var i = 0; i < bars.Count; i++)
                    {
                        // Deliberately adverse previews may not change the next final bar.
                        var alternate = new Bar(bars[i].Time, -1, 1, -1, -1, 1);
                        state.Update(Native(alternate), false, true);
                        Assert.Equal(expected[i], state.Update(Native(bars[i]), false, true).Value);
                        Assert.Equal(expected[i], state.Update(Native(bars[i]), true, true).Value);
                    }
                }
            }
        }
    }

    [Fact]
    public async Task SelectedInputControlsBreakoutsAndOppositeEventsResetCounts()
    {
        var bars = new[] { 1d, 2, 4, -1, -2, 3, 4 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 1)).ToArray();
        var expected = new[] { 0d, .25, .5, -.375, 1 - 4.5 / 5, .5 - 5d / 6, 1 - 6d / 7 };
        Assert.Equal(expected, Data(bars).CalculateTrendForceHistogram(2).CustomValuesList);
        Assert.Equal(expected, BuiltInFormulaReferences.RoundedTrendForceReference(bars, 2));
        var source = new Sma(2);
        var indicator = new TrendForceHistogram(2).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, selected[i], selected[i], selected[i], selected[i], b.Volume)).ToArray();
        var selectedExpected = BuiltInFormulaReferences.RoundedTrendForceReference(projected, 2);
        Assert.Equal(selectedExpected, run[indicator].ToArray());
        var data = Data(bars);
        data.CustomValuesList = selected.ToList();
        Assert.Equal(selectedExpected, data.CalculateTrendForceHistogram(2).CustomValuesList);
    }
}

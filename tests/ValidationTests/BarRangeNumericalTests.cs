using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class BarRangeNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(TrueRange) || c.IndicatorType == typeof(OoplesFinance.StockIndicators.Indicators.Range)).Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesNumericalAndSignedOverflowCases(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
        foreach (var sign in new[] { "positive", "negative" })
            Assert.Equal(2, Assert.Single(report.FixtureEvidence,
                f => f.Name == "range-first-" + sign + "-output-overflow").OutputOverflowRejectionsChecked);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EveryRouteMatchesTheIndependentRangeIncludingPreviewAndReset(bool trueRange)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244).Concat(new[] {
            new IndicatorValidationFixture("reversed-and-gaps", new[] {
                new Bar(DateTime.UnixEpoch, 0, -2, 3, 0, 1),
                new Bar(DateTime.UnixEpoch.AddMinutes(1), 0, -4, 5, 20, 1),
                new Bar(DateTime.UnixEpoch.AddMinutes(2), 0, 3, 1, -20, 1),
                new Bar(DateTime.UnixEpoch.AddMinutes(3), 0, 2, -3, 0, 1) }) }))
        {
            var bars = fixture.Bars;
            var expected = BuiltInFormulaReferences.RoundedBarRange(bars, trueRange);
            var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            if (trueRange) data.CalculateTrueRange(); else data.CalculateRange();
            Assert.Equal(expected, data.OutputValues[trueRange ? "TrueRange" : "Range"]);
            var core = new double[bars.Count];
            if (trueRange) VolatilityCore.TrueRange(bars.Select(b => b.High).ToArray(), bars.Select(b => b.Low).ToArray(),
                bars.Select(b => b.Close).ToArray(), core);
            else TrendCore.Range(bars.Select(b => b.High).ToArray(), bars.Select(b => b.Low).ToArray(), core);
            Assert.Equal(expected, core);
            IIndicator indicator = trueRange ? new TrueRange(14) : new OoplesFinance.StockIndicators.Indicators.Range(14);
            if (expected.Any(double.IsInfinity))
                await Assert.ThrowsAsync<IndicatorOutputException>(() => new StockIndicatorBuilder()
                    .ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync());
            else
            {
                using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
                Assert.Equal(expected, run[indicator].ToArray());
            }
            var builtIn = (IBuiltInIndicator)indicator;
            var spec = IndicatorSpecs.Create(builtIn.BatchName, builtIn.CreateOptions());
            foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
            {
                Assert.NotNull(state);
                using var lifetime = state as IDisposable;
                for (var replay = 0; replay < 2; replay++)
                {
                    state.Reset();
                    for (var i = 0; i < bars.Count; i++)
                    foreach (var commit in new[] { false, true })
                    {
                        var b = bars[i];
                        var native = new OhlcvBar("FACILITATION", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                        Assert.Equal(expected[i], state.Update(native, commit, true).Value);
                    }
                }
            }
        }
    }

    [Fact]
    public async Task SelectedPreviousCloseAndUpdatedOhlcColumnsAreUsed()
    {
        var bars = Enumerable.Range(0, 4).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 100, 104, 98, 102, 1)).ToArray();
        var source = new Sma(2);
        var indicator = new TrueRange(14).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        var projected = run[source].ToArray().Select((v, i) => new Bar(bars[i].Time, 100, 104, 98, v, 1)).ToArray();
        Assert.Equal(BuiltInFormulaReferences.RoundedBarRange(projected, true), run[indicator].ToArray());
        var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
            bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
        _ = data.TickerDataList;
        data.HighPrices = new() { 4, 4, 4, 4 };
        data.LowPrices = new() { 1, 1, 1, 1 };
        data.InputValues = new() { 20, -20, 2, 2 };
        using var context = new OoplesFinance.StockIndicators.Builder.Compute.ComputeContext();
        using var result = OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.ComputeTrueRangeFast(data, context);
        Assert.Equal(new[] { 3d, 19, 24, 3 }, result.ToArray());
    }
}

using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class MarketFacilitationNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(MarketFacilitationIndex)).Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesNumericalAndSignedOverflowCases(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
        foreach (var sign in new[] { "positive", "negative" })
            Assert.Equal(2, Assert.Single(report.FixtureEvidence,
                f => f.Name == "market-facilitation-" + sign + "-output-overflow").OutputOverflowRejectionsChecked);
    }

    [Fact]
    public void FinalRatioHandlesOverflowingRangesAndSignedVolumes()
    {
        Assert.Equal(2, RoundedRangeRatio.Of(double.MaxValue, -double.MaxValue, double.MaxValue));
        Assert.Equal(-2, RoundedRangeRatio.Of(double.MaxValue, -double.MaxValue, -double.MaxValue));
        Assert.Equal(-2, RoundedRangeRatio.Of(-double.MaxValue, double.MaxValue, double.MaxValue));
        Assert.Equal(0, RoundedRangeRatio.Of(double.MaxValue, -double.MaxValue, 0));
        Assert.Equal(2, RoundedRangeRatio.Of(double.Epsilon, -double.Epsilon, double.Epsilon));
    }

    [Fact]
    public async Task EveryRouteMatchesTheIndependentRangeRatioIncludingPreviewAndReset()
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        {
            var bars = fixture.Bars;
            var expected = BuiltInFormulaReferences.RoundedMarketFacilitation(bars);
            var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            data.CalculateMarketFacilitationIndex();
            Assert.Equal(expected, data.OutputValues["Mi"]);
            var core = new double[bars.Count];
            OscillatorCore.MarketFacilitationIndex(bars.Select(b => b.High).ToArray(), bars.Select(b => b.Low).ToArray(),
                bars.Select(b => b.Volume).ToArray(), core);
            Assert.Equal(expected, core);
            var indicator = new MarketFacilitationIndex(14);
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
    public async Task TypedPriceChainingKeepsOriginalRangeAndVolume()
    {
        var bars = Enumerable.Range(0, 8).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i),
            100 + i, 104 + i, 98 + i, 102 + i, i + 1)).ToArray();
        var source = new Sma(3);
        var indicator = new MarketFacilitationIndex(14).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(BuiltInFormulaReferences.RoundedMarketFacilitation(bars), run[indicator].ToArray());
    }
}

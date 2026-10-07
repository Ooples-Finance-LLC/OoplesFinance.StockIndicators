using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class ExtendedReversalTests
{
    public static IEnumerable<object[]> Cases =>
        Enumerable
            .Range(0, ExtendedReversalComparison.Cases.Length)
            .Select(i => new object[] { i });
    public static IEnumerable<object[]> Names =>
        ExtendedReversalComparison.Names.Select(n => new object[] { n });

    [Theory, MemberData(nameof(Cases))]
    public async Task HandCalculatedColorsGapEndpointsShadowsAndContainment(int index)
    {
        var c = ExtendedReversalComparison.Cases[index];
        var actual = await Run(ExtendedReversalComparison.Create(c.Name), c.Bars);
        Assert.Equal(c.Expected, actual[^1]);
        Assert.All(actual.Take(actual.Length - 1), v => Assert.Equal(0, v));
        var data = CrowSoldierComparison.FromBars(c.Bars);
        var pair = ComparisonPairs.Get("TaLib.Candles." + c.Name);
        Assert.Equal(c.Expected, pair.Competitor(data, 20).Outputs["Value"].Values[^1]);
        Assert.Equal(c.Expected, pair.Reference!(data, 20).Outputs["Value"].Values[^1]);
    }

    [Theory, MemberData(nameof(Names))]
    public async Task ConfigurableWindowsIndependentReferenceAndLifecycle(string name)
    {
        foreach (var period in new[] { 1, 3, 10 })
        {
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    ExtendedReversalComparison.Create(name).GetType(),
                    "window",
                    () => ExtendedReversalComparison.Create(name, period)
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
    }

    [Theory, MemberData(nameof(Names))]
    public async Task ExtremeAndSubnormalScalingPreservesSignals(string name)
    {
        var c = ExtendedReversalComparison.Cases.First(c => c.Name == name && c.Expected != 0);
        foreach (var scale in new[] { 8 * double.Epsilon, Math.ScaleB(1d, 1019) })
        {
            var bars = c
                .Bars.Select(b => new Bar(
                    b.Time,
                    b.Open * scale,
                    b.High * scale,
                    b.Low * scale,
                    b.Close * scale,
                    1
                ))
                .ToArray();
            Assert.Equal(
                c.Expected,
                (await Run(ExtendedReversalComparison.Create(name), bars))[^1]
            );
        }
    }

    [Theory, MemberData(nameof(Names))]
    public async Task PeriodLimitsAndLazyStorage(string name)
    {
        var limit = int.MaxValue - (name == "ConcealingBabySwallow" ? 3 : 4);
        foreach (var invalid in new[] { 0, -1, limit + 1 })
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ExtendedReversalComparison.Create(name, invalid)
            );
        var indicator = ExtendedReversalComparison.Create(name, limit);
        Assert.Equal(int.MaxValue, indicator.WarmupBars);
        Assert.Equal(
            new double[1],
            await Run(indicator, [new Bar(DateTime.UnixEpoch, 4, 10, 0, 6, 1)])
        );
    }

    [Fact]
    public void PinnedLookbacksAndThresholdSettings()
    {
        Assert.Equal(14, TALib.Candles.BreakawayLookback());
        Assert.Equal(13, TALib.Candles.ConcealingBabySwallowLookback());
        Assert.Equal(14, TALib.Candles.LadderBottomLookback());
        Assert.Equal(
            .1,
            TALib.Core.CandleSettings.Get(TALib.Core.CandleSettingType.ShadowVeryShort).Factor
        );
        Assert.Equal(
            1,
            TALib.Core.CandleSettings.Get(TALib.Core.CandleSettingType.BodyLong).Factor
        );
    }

    [Fact]
    public void AddedFixtureAcrossExistingCandlePairs()
    {
        foreach (var pair in ComparisonPairs.All.Where(p => p.IsCandle))
            ComparisonVerifier.Check(pair, ExtendedReversalComparison.Fixture(), 20);
    }

    private static async Task<double[]> Run(IIndicator indicator, Bar[] bars)
    {
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        return run[indicator.Outputs[0]].ToArray();
    }
}

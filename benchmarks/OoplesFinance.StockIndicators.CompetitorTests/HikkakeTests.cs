using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class HikkakeTests
{
    public static IEnumerable<object[]> Cases =>
        Enumerable.Range(0, HikkakeComparison.Cases.Length).Select(i => new object[] { i });
    public static IEnumerable<object[]> Names =>
        HikkakeComparison.Names.Select(n => new object[] { n });

    [Theory, MemberData(nameof(Cases))]
    public async Task FormationConfirmationExpiryPriorityAndWarmupPriming(int index)
    {
        var c = HikkakeComparison.Cases[index];
        Assert.Equal(c.Expected, await Run(HikkakeComparison.Create(c.Name), c.Bars));
        var data = CrowSoldierComparison.FromBars(c.Bars);
        var pair = ComparisonPairs.Get("TaLib.Candles." + c.Name);
        Assert.Equal(c.Expected, pair.Competitor(data, 20).Outputs["Value"].Values);
        Assert.Equal(c.Expected, pair.Reference!(data, 20).Outputs["Value"].Values);
    }

    [Theory, MemberData(nameof(Names))]
    public async Task IndependentReferencesConfigurableNearPeriodsAndLifecycle(string name)
    {
        foreach (var period in new[] { 1, 3, 5, 10 })
        {
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    HikkakeComparison.Create(name).GetType(),
                    "window",
                    () => HikkakeComparison.Create(name, period)
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
            if (name == "Hikkake")
                break;
        }
    }

    [Theory, MemberData(nameof(Names))]
    public async Task ExtremeAndSubnormalScalingPreservesFullSignalSequence(string name)
    {
        var c = HikkakeComparison.Cases.First(c => c.Name == name);
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
            Assert.Equal(c.Expected, await Run(HikkakeComparison.Create(name), bars));
        }
    }

    [Fact]
    public async Task ModifiedPeriodLimitsAndLazyStorage()
    {
        var limit = int.MaxValue - 5;
        foreach (var invalid in new[] { 0, -1, limit + 1 })
            Assert.Throws<ArgumentOutOfRangeException>(() => new ModifiedHikkakeCandle(invalid));
        var indicator = new ModifiedHikkakeCandle(limit);
        Assert.Equal(int.MaxValue, indicator.WarmupBars);
        Assert.Equal(
            new double[1],
            await Run(indicator, [new Bar(DateTime.UnixEpoch, 4, 10, 0, 6, 1)])
        );
    }

    [Fact]
    public void PinnedLookbacksAndNearSettings()
    {
        Assert.Equal(5, TALib.Candles.HikkakeLookback());
        Assert.Equal(10, TALib.Candles.HikkakeModifiedLookback());
        Assert.Equal(.2, TALib.Core.CandleSettings.Get(TALib.Core.CandleSettingType.Near).Factor);
    }

    [Fact]
    public void AddedFixtureAcrossExistingCandlePairs()
    {
        foreach (var pair in ComparisonPairs.All.Where(p => p.IsCandle))
            ComparisonVerifier.Check(pair, HikkakeComparison.Fixture(), 20);
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

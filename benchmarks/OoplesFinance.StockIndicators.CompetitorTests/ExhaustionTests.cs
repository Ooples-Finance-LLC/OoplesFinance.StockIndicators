using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class ExhaustionTests
{
    public static IEnumerable<object[]> Cases =>
        Enumerable.Range(0, ExhaustionComparison.Cases.Length).Select(i => new object[] { i });
    public static IEnumerable<object[]> Names =>
        ExhaustionComparison.Names.Select(n => new object[] { n });

    [Theory, MemberData(nameof(Cases))]
    public async Task HandCalculatedExhaustionBranchesAndThresholdBoundaries(int index)
    {
        var c = ExhaustionComparison.Cases[index];
        var actual = await Run(ExhaustionComparison.Create(c.Name), c.Bars);
        Assert.Equal(c.Expected, actual[^1]);
        Assert.All(actual.Take(actual.Length - 1), v => Assert.Equal(0, v));
        var data = CrowSoldierComparison.FromBars(c.Bars);
        var pair = ComparisonPairs.Get("TaLib.Candles." + c.Name);
        Assert.Equal(c.Expected, pair.Reference!(data, 20).Outputs["Value"].Values[^1]);
        Assert.Equal(c.Expected, pair.Competitor(data, 20).Outputs["Value"].Values[^1]);
    }

    [Theory, MemberData(nameof(Names))]
    public async Task IndependentReferencesUnequalWindowsAndLifecycle(string name)
    {
        foreach (
            var (a, b, c, d, e) in new[]
            {
                (1, 1, 1, 1, 1),
                (1, 3, 2, 4, 5),
                (3, 1, 4, 2, 1),
                (10, 10, 10, 5, 5),
            }
        )
        {
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    ExhaustionComparison.Create(name).GetType(),
                    "windows",
                    () => ExhaustionComparison.Create(name, a, b, c, d, e)
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
    }

    [Theory, MemberData(nameof(Names))]
    public async Task ExtremeAndSubnormalPricesPreserveSignals(string name)
    {
        var c = ExhaustionComparison.Cases.First(c => c.Name == name && c.Expected != 0);
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
            Assert.Equal(c.Expected, (await Run(ExhaustionComparison.Create(name), bars))[^1]);
        }
    }

    [Theory, MemberData(nameof(Names))]
    public async Task PeriodLimitsAndLazyStorage(string name)
    {
        var positions =
            name == "AdvanceBlock" ? new[] { 0, 2, 3, 4 }
            : name == "Stalled" ? new[] { 0, 1, 2, 3 }
            : new[] { 0, 1, 2 };
        var limit = int.MaxValue - 2;
        foreach (var position in positions)
        {
            foreach (var invalid in new[] { 0, -1, limit + 1 })
            {
                var p = new[] { 1, 1, 1, 1, 1 };
                p[position] = invalid;
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    ExhaustionComparison.Create(name, p[0], p[1], p[2], p[3], p[4])
                );
            }
            var periods = new[] { 1, 1, 1, 1, 1 };
            periods[position] = limit;
            var indicator = ExhaustionComparison.Create(
                name,
                periods[0],
                periods[1],
                periods[2],
                periods[3],
                periods[4]
            );
            Assert.Equal(int.MaxValue, indicator.WarmupBars);
            Assert.Equal(
                new double[1],
                await Run(indicator, [new Bar(DateTime.UnixEpoch, 4, 10, 0, 6, 1)])
            );
        }
    }

    [Fact]
    public void PinnedLookbacksAndShadowDefinitions()
    {
        Assert.Equal(12, TALib.Candles.AdvanceBlockLookback());
        Assert.Equal(12, TALib.Candles.StalledLookback());
        Assert.Equal(12, TALib.Candles.ThreeStarsInSouthLookback());
        var longShadow = TALib.Core.CandleSettings.Get(TALib.Core.CandleSettingType.ShadowLong);
        Assert.Equal(TALib.Core.CandleRangeType.RealBody, longShadow.RangeType);
        Assert.Equal(0, longShadow.AveragePeriod);
        Assert.Equal(1, longShadow.Factor);
        var shortShadow = TALib.Core.CandleSettings.Get(TALib.Core.CandleSettingType.ShadowShort);
        Assert.Equal(TALib.Core.CandleRangeType.Shadows, shortShadow.RangeType);
        Assert.Equal(10, shortShadow.AveragePeriod);
        Assert.Equal(1, shortShadow.Factor);
    }

    [Fact]
    public void AddedFixtureAcrossExistingCandlePairs()
    {
        foreach (var pair in ComparisonPairs.All.Where(p => p.IsCandle))
            ComparisonVerifier.Check(pair, ExhaustionComparison.Fixture(), 20);
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

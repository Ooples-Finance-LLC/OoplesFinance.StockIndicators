using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class GapContinuationTests
{
    public static IEnumerable<object[]> Cases =>
        Enumerable.Range(0, GapContinuationComparison.Cases.Length).Select(i => new object[] { i });
    public static IEnumerable<object[]> Names =>
        GapContinuationComparison.Names.Select(n => new object[] { n });

    [Theory, MemberData(nameof(Cases))]
    public async Task HandCalculatedGapThresholdAndContainmentBoundaries(int index)
    {
        var c = GapContinuationComparison.Cases[index];
        var actual = await Run(
            GapContinuationComparison.Create(
                c.Name,
                c.Name == "TasukiGap" ? 5 : 10,
                penetration: c.Penetration
            ),
            c.Bars
        );
        Assert.Equal(c.Expected, actual[^1]);
        Assert.All(actual.Take(actual.Length - 1), v => Assert.Equal(0, v));
        var data = CrowSoldierComparison.FromBars(c.Bars);
        Assert.Equal(
            c.Expected,
            GapContinuationComparison
                .Competitor(c.Name, data, c.Penetration)
                .Outputs["Value"]
                .Values[^1]
        );
        Assert.Equal(
            c.Expected,
            GapContinuationComparison
                .Reference(c.Name, data, c.Penetration)
                .Outputs["Value"]
                .Values[^1]
        );
    }

    [Theory, MemberData(nameof(Names))]
    public async Task UnequalPeriodsIndependentReferenceAndLifecycle(string name)
    {
        foreach (
            var (a, b, c, p) in new[]
            {
                (1, 1, 1, 0d),
                (1, 3, 2, .5),
                (3, 1, 4, 1d),
                (10, 10, 10, .3),
            }
        )
        {
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    GapContinuationComparison.Create(name).GetType(),
                    "windows",
                    () => GapContinuationComparison.Create(name, a, b, c, p)
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
            if (name == "UpDownSideGapThreeMethods")
                break;
        }
    }

    [Fact]
    public void AbandonedBabyPenetrationConfigurationsAgainstPackageAndIndependentFormula()
    {
        var data = GapContinuationComparison.Fixture();
        foreach (var p in new[] { 0d, .3, .5, 1d, 2d })
        {
            var expected = GapContinuationComparison
                .Reference("AbandonedBaby", data, p)
                .Outputs["Value"]
                .Values;
            Assert.Equal(
                expected,
                GapContinuationComparison.Ooples("AbandonedBaby", data, p).Outputs["Value"].Values
            );
            Assert.Equal(
                GapContinuationComparison
                    .Reference("AbandonedBaby", data, p, competitorArithmetic: true)
                    .Outputs["Value"]
                    .Values,
                GapContinuationComparison
                    .Competitor("AbandonedBaby", data, p)
                    .Outputs["Value"]
                    .Values
            );
        }
    }

    [Theory, MemberData(nameof(Names))]
    public async Task ExtremeAndSubnormalScalingPreservesSignals(string name)
    {
        var c = GapContinuationComparison.Cases.First(c => c.Name == name && c.Expected != 0);
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
                (
                    await Run(
                        GapContinuationComparison.Create(
                            name,
                            name == "TasukiGap" ? 5 : 10,
                            penetration: c.Penetration
                        ),
                        bars
                    )
                )[^1]
            );
        }
        if (name == "AbandonedBaby")
            Assert.Equal(
                0,
                (
                    await Run(
                        GapContinuationComparison.Create(name, penetration: double.MaxValue),
                        c.Bars
                    )
                )[^1]
            );
    }

    [Theory, MemberData(nameof(Names))]
    public async Task PeriodLimitsLazyStorageAndParameterValidation(string name)
    {
        var limit = int.MaxValue - 2;
        var n =
            name == "AbandonedBaby" ? 3
            : name == "UpDownSideGapThreeMethods" ? 0
            : 1;
        for (var position = 0; position < n; position++)
        {
            foreach (var invalid in new[] { 0, -1, limit + 1 })
            {
                var p = new[] { 1, 1, 1 };
                p[position] = invalid;
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    GapContinuationComparison.Create(name, p[0], p[1], p[2])
                );
            }
            var periods = new[] { 1, 1, 1 };
            periods[position] = limit;
            var indicator = GapContinuationComparison.Create(
                name,
                periods[0],
                periods[1],
                periods[2]
            );
            Assert.Equal(int.MaxValue, indicator.WarmupBars);
            Assert.Equal(
                new double[1],
                await Run(indicator, [new Bar(DateTime.UnixEpoch, 4, 10, 0, 6, 1)])
            );
        }
        if (name == "AbandonedBaby")
            foreach (
                var p in new[] { -1, double.NaN, double.NegativeInfinity, double.PositiveInfinity }
            )
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    GapContinuationComparison.Create(name, penetration: p)
                );
    }

    [Fact]
    public void PinnedLookbacksAndThresholdSettings()
    {
        Assert.Equal(12, TALib.Candles.AbandonedBabyLookback());
        Assert.Equal(12, TALib.Candles.TristarLookback());
        Assert.Equal(7, TALib.Candles.TasukiGapLookback());
        Assert.Equal(2, TALib.Candles.UpDownSideGapThreeMethodsLookback());
        Assert.Equal(
            .1,
            TALib.Core.CandleSettings.Get(TALib.Core.CandleSettingType.BodyDoji).Factor
        );
        Assert.Equal(.2, TALib.Core.CandleSettings.Get(TALib.Core.CandleSettingType.Near).Factor);
    }

    [Fact]
    public void AddedFixtureAcrossExistingCandlePairs()
    {
        foreach (var pair in ComparisonPairs.All.Where(p => p.IsCandle))
            ComparisonVerifier.Check(pair, GapContinuationComparison.Fixture(), 20);
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

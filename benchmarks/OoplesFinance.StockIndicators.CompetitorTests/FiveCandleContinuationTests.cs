using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class FiveCandleContinuationTests
{
    public static IEnumerable<object[]> Cases =>
        Enumerable
            .Range(0, FiveCandleContinuationComparison.Cases.Length)
            .Select(i => new object[] { i });
    public static IEnumerable<object[]> Names =>
        FiveCandleContinuationComparison.Names.Select(n => new object[] { n });

    [Theory, MemberData(nameof(Cases))]
    public async Task HandCalculatedBodyThresholdsColorsOverlapGapsAndPenetration(int index)
    {
        var c = FiveCandleContinuationComparison.Cases[index];
        var actual = await Run(
            FiveCandleContinuationComparison.Create(c.Name, penetration: c.Penetration),
            c.Bars
        );
        Assert.Equal(c.Expected, actual[^1]);
        Assert.All(actual.Take(actual.Length - 1), v => Assert.Equal(0, v));
        var data = CrowSoldierComparison.FromBars(c.Bars);
        Assert.Equal(
            c.Expected,
            FiveCandleContinuationComparison
                .Reference(c.Name, data, c.Penetration)
                .Outputs["Value"]
                .Values[^1]
        );
        Assert.Equal(
            c.RoundedExpected ?? c.Expected,
            FiveCandleContinuationComparison
                .Competitor(c.Name, data, c.Penetration)
                .Outputs["Value"]
                .Values[^1]
        );
        Assert.Equal(
            c.RoundedExpected ?? c.Expected,
            FiveCandleContinuationComparison
                .Reference(c.Name, data, c.Penetration, true)
                .Outputs["Value"]
                .Values[^1]
        );
    }

    [Theory, MemberData(nameof(Names))]
    public async Task IndependentReferencesUnequalWindowsAndLifecycle(string name)
    {
        foreach (var (a, b, p) in new[] { (1, 1, 0d), (1, 3, .1), (3, 1, .5), (10, 10, 2d) })
        {
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    FiveCandleContinuationComparison.Create(name).GetType(),
                    "windows",
                    () => FiveCandleContinuationComparison.Create(name, a, b, p)
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
    }

    [Fact]
    public void MatHoldPenetrationConfigurationsVerifyBothArithmeticContracts()
    {
        var data = FiveCandleContinuationComparison.Fixture();
        foreach (var p in new[] { 0d, .1, .3, .5, 1d, 2d })
        {
            Assert.Equal(
                FiveCandleContinuationComparison
                    .Reference("MatHold", data, p)
                    .Outputs["Value"]
                    .Values,
                FiveCandleContinuationComparison.Ooples("MatHold", data, p).Outputs["Value"].Values
            );
            Assert.Equal(
                FiveCandleContinuationComparison
                    .Reference("MatHold", data, p, true)
                    .Outputs["Value"]
                    .Values,
                FiveCandleContinuationComparison
                    .Competitor("MatHold", data, p)
                    .Outputs["Value"]
                    .Values
            );
        }
    }

    [Theory, MemberData(nameof(Names))]
    public async Task ExtremeAndSubnormalPricesPreserveSignals(string name)
    {
        var c = FiveCandleContinuationComparison.Cases.First(c =>
            c.Name == name && c.Expected != 0
        );
        foreach (var scale in new[] { 8 * double.Epsilon, Math.ScaleB(1d, 1020) })
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
                (await Run(FiveCandleContinuationComparison.Create(name), bars))[^1]
            );
        }
        if (name == "MatHold")
            Assert.Equal(
                100,
                (
                    await Run(
                        FiveCandleContinuationComparison.Create(name, penetration: double.MaxValue),
                        c.Bars
                    )
                )[^1]
            );
    }

    [Theory, MemberData(nameof(Names))]
    public async Task PeriodLimitsLazyStorageAndPenetrationValidation(string name)
    {
        var limit = int.MaxValue - 4;
        foreach (var invalid in new[] { 0, -1, limit + 1 })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                FiveCandleContinuationComparison.Create(name, invalid, 1)
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                FiveCandleContinuationComparison.Create(name, 1, invalid)
            );
        }
        foreach (var (a, b) in new[] { (limit, 1), (1, limit) })
        {
            var indicator = FiveCandleContinuationComparison.Create(name, a, b);
            Assert.Equal(int.MaxValue, indicator.WarmupBars);
            Assert.Equal(
                new double[1],
                await Run(indicator, [new Bar(DateTime.UnixEpoch, 4, 10, 0, 6, 1)])
            );
        }
        if (name == "MatHold")
            foreach (
                var p in new[] { -1, double.NaN, double.NegativeInfinity, double.PositiveInfinity }
            )
                Assert.Throws<ArgumentOutOfRangeException>(() => new MatHoldCandle(penetration: p));
    }

    [Fact]
    public void PinnedLookbacksAndBodyThresholds()
    {
        Assert.Equal(14, TALib.Candles.MatHoldLookback());
        Assert.Equal(14, TALib.Candles.RisingFallingThreeMethodsLookback());
        Assert.Equal(
            10,
            TALib.Core.CandleSettings.Get(TALib.Core.CandleSettingType.BodyLong).AveragePeriod
        );
        Assert.Equal(
            10,
            TALib.Core.CandleSettings.Get(TALib.Core.CandleSettingType.BodyShort).AveragePeriod
        );
    }

    [Fact]
    public void AddedFixtureAcrossExistingCandlePairs()
    {
        foreach (var pair in ComparisonPairs.All.Where(p => p.IsCandle))
            ComparisonVerifier.Check(pair, FiveCandleContinuationComparison.Fixture(), 20);
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

using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class CrowSoldierTests
{
    public static IEnumerable<object[]> Cases =>
        Enumerable.Range(0, CrowSoldierComparison.Cases.Length).Select(i => new object[] { i });
    public static IEnumerable<object[]> Names =>
        CrowSoldierComparison.Names.Select(n => new object[] { n });

    [Theory, MemberData(nameof(Cases))]
    public async Task HandCalculatedColorsContainmentAndToleranceBoundaries(int index)
    {
        var c = CrowSoldierComparison.Cases[index];
        var actual = await Run(CrowSoldierComparison.Create(c.Name), c.Bars);
        Assert.Equal(c.Expected, actual[^1]);
        Assert.All(actual.Take(actual.Length - 1), v => Assert.Equal(0, v));
        var data = CrowSoldierComparison.FromBars(c.Bars);
        var pair = ComparisonPairs.Get("TaLib.Candles." + c.Name);
        Assert.Equal(c.Expected, pair.Competitor(data, 20).Outputs["Value"].Values[^1]);
        Assert.Equal(c.Expected, pair.Reference!(data, 20).Outputs["Value"].Values[^1]);
    }

    [Theory, MemberData(nameof(Names))]
    public async Task EachShadowMustBeStrictlyShorter(string name)
    {
        var c = CrowSoldierComparison.Cases.First(c => c.Name == name && c.Expected != 0);
        for (var position = 0; position < 3; position++)
        {
            var bars = c.Bars.ToArray();
            var i = bars.Length - 3 + position;
            var b = bars[i];
            var white = name == "ThreeWhiteSoldiers";
            bars[i] = Make(
                i,
                b.Open,
                white ? b.Close + 1 : b.Close + 9,
                white ? b.Close - 9 : b.Close - 1,
                b.Close
            );
            Assert.Equal(0, (await Run(CrowSoldierComparison.Create(name), bars))[^1]);
            var data = CrowSoldierComparison.FromBars(bars);
            var pair = ComparisonPairs.Get("TaLib.Candles." + name);
            Assert.Equal(0, pair.Competitor(data, 20).Outputs["Value"].Values[^1]);
            Assert.Equal(0, pair.Reference!(data, 20).Outputs["Value"].Values[^1]);
        }
    }

    [Theory]
    [InlineData("IdenticalThreeCrows")]
    [InlineData("ThreeWhiteSoldiers")]
    public async Task PrecedingCandleMayBeBlack(string name)
    {
        var c = CrowSoldierComparison.Cases.First(c => c.Name == name && c.Expected != 0);
        var bars = c.Bars.ToArray();
        bars[9] = Make(9, 6, 10, 0, 4);
        Assert.Equal(c.Expected, (await Run(CrowSoldierComparison.Create(name), bars))[^1]);
        Assert.Equal(
            c.Expected,
            ComparisonPairs
                .Get("TaLib.Candles." + name)
                .Competitor(CrowSoldierComparison.FromBars(bars), 20)
                .Outputs["Value"]
                .Values[^1]
        );
    }

    [Theory, MemberData(nameof(Names))]
    public async Task IndependentReferencesUnequalWindowsAndLifecycle(string name)
    {
        foreach (
            var (a, b, c, d) in new[] { (1, 1, 1, 1), (1, 3, 2, 4), (3, 1, 4, 2), (10, 5, 5, 10) }
        )
        {
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    CrowSoldierComparison.Create(name).GetType(),
                    "windows",
                    () => CrowSoldierComparison.Create(name, a, b, c, d)
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
    }

    [Theory, MemberData(nameof(Names))]
    public async Task ExtremeAndSubnormalScalingPreservesSignals(string name)
    {
        var c = CrowSoldierComparison.Cases.First(c => c.Name == name && c.Expected != 0);
        foreach (var scale in new[] { 8 * double.Epsilon, Math.ScaleB(1d, 1019) })
        {
            var bars = c
                .Bars.Select(
                    (b, i) =>
                        Make(i, b.Open * scale, b.High * scale, b.Low * scale, b.Close * scale)
                )
                .ToArray();
            Assert.Equal(c.Expected, (await Run(CrowSoldierComparison.Create(name), bars))[^1]);
        }
    }

    [Theory, MemberData(nameof(Names))]
    public async Task ParameterValidationAndLazyStorage(string name)
    {
        var limit = int.MaxValue - (name == "ThreeBlackCrows" ? 3 : 2);
        var parameterCount =
            name == "ThreeBlackCrows" ? 1
            : name == "IdenticalThreeCrows" ? 2
            : 4;
        for (var position = 0; position < parameterCount; position++)
        {
            foreach (var invalid in new[] { 0, -1, limit + 1 })
            {
                var p = new[] { 1, 1, 1, 1 };
                p[position] = invalid;
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    CrowSoldierComparison.Create(name, p[0], p[1], p[2], p[3])
                );
            }
            var periods = new[] { 1, 1, 1, 1 };
            periods[position] = limit;
            var indicator = CrowSoldierComparison.Create(
                name,
                periods[0],
                periods[1],
                periods[2],
                periods[3]
            );
            Assert.Equal(int.MaxValue, indicator.WarmupBars);
            Assert.Equal(new double[1], await Run(indicator, [Make(0, 4, 10, 0, 6)]));
        }
    }

    [Fact]
    public void PinnedLookbacksAndToleranceSettings()
    {
        Assert.Equal(13, TALib.Candles.ThreeBlackCrowsLookback());
        Assert.Equal(12, TALib.Candles.IdenticalThreeCrowsLookback());
        Assert.Equal(12, TALib.Candles.ThreeWhiteSoldiersLookback());
        Assert.Equal(.05, TALib.Core.CandleSettings.Get(TALib.Core.CandleSettingType.Equal).Factor);
        Assert.Equal(.2, TALib.Core.CandleSettings.Get(TALib.Core.CandleSettingType.Near).Factor);
        Assert.Equal(.6, TALib.Core.CandleSettings.Get(TALib.Core.CandleSettingType.Far).Factor);
        Assert.Equal(
            .1,
            TALib.Core.CandleSettings.Get(TALib.Core.CandleSettingType.ShadowVeryShort).Factor
        );
    }

    [Fact]
    public void AddedFixtureAcrossExistingCandlePairs()
    {
        foreach (var pair in ComparisonPairs.All.Where(p => p.IsCandle))
            ComparisonVerifier.Check(pair, CrowSoldierComparison.Fixture(), 20);
    }

    private static Bar Make(int i, double o, double h, double l, double c) =>
        new(DateTime.UnixEpoch.AddDays(i), o, h, l, c, 1);

    private static async Task<double[]> Run(IIndicator indicator, Bar[] bars)
    {
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        return run[indicator.Outputs[0]].ToArray();
    }
}

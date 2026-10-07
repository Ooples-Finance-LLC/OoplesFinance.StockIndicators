using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class KickingRickshawTests
{
    public static IEnumerable<object[]> Names =>
        KickingRickshawComparison.Names.Select(n => new object[] { n });

    [Theory]
    [InlineData("Kicking")]
    [InlineData("KickingByLength")]
    public async Task GoldenGapsShadowsBodyLengthsAndTieSelection(string name)
    {
        for (var i = 0; i < KickingRickshawComparison.KickingCandidates.Length; i++)
            foreach (var mirror in new[] { false, true })
            {
                var expected =
                    i < 3
                        ? name == "Kicking" || i == 1
                            ? 100d
                            : -100
                        : 0;
                if (mirror)
                    expected = -expected;
                await Golden(name, KickingRickshawComparison.KickingBars(i, mirror), expected);
            }
    }

    [Fact]
    public async Task RickshawInclusiveMidpointBandDojiAndStrictShadows()
    {
        for (var i = 0; i < KickingRickshawComparison.RickshawCandidates.Length; i++)
            foreach (var reverse in new[] { false, true })
                await Golden(
                    "RickshawMan",
                    KickingRickshawComparison.RickshawBars(i, reverse),
                    KickingRickshawComparison.RickshawCandidates[i].Expected
                );
    }

    private static async Task Golden(string name, Bar[] bars, double expected)
    {
        var actual = await Run(
            KickingRickshawComparison.Create(name, 10, name == "RickshawMan" ? 5 : 10),
            bars
        );
        Assert.All(actual.Take(actual.Length - 1), v => Assert.Equal(0, v));
        Assert.Equal(expected, actual[^1]);
        var pair = ComparisonPairs.Get("TaLib.Candles." + name);
        var data = KickingRickshawComparison.FromBars(bars);
        Assert.Equal(expected, pair.Competitor(data, 20).Outputs["Value"].Values[^1]);
        Assert.Equal(expected, pair.Reference!(data, 20).Outputs["Value"].Values[^1]);
    }

    [Theory, MemberData(nameof(Names))]
    public async Task IndependentContractsWithUnequalPeriodsAndLifecycle(string name)
    {
        foreach (var (p, q) in new[] { (1, 1), (1, 3), (3, 1), (10, 5), (10, 10) })
        {
            var indicator = KickingRickshawComparison.Create(name, p, q);
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    indicator.GetType(),
                    "thresholds",
                    () => KickingRickshawComparison.Create(name, p, q)
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
    }

    [Theory]
    [InlineData("Kicking")]
    [InlineData("KickingByLength")]
    public async Task BothLongBodiesAndBothPriorShadowsUseStrictThresholds(string name)
    {
        foreach (var upper in new[] { false, true })
        {
            var bars = KickingRickshawComparison.KickingBars(1);
            bars[10] = Make(10, 10, upper ? 11 : 10, upper ? 0 : -1, 0);
            await Golden(name, bars, 0);
        }
        var firstTie = KickingRickshawComparison.KickingBars(1);
        for (var i = 0; i < 10; i++)
            firstTie[i] = Make(i, 0, 10, 0, 10);
        await Golden(name, firstTie, 0);
        var secondTie = KickingRickshawComparison.KickingBars(1);
        secondTie[10] = Make(10, 12, 12, 0, 0);
        secondTie[11] = Make(11, 14, 17, 14, 17);
        await Golden(name, secondTie, 0);
    }

    [Theory, MemberData(nameof(Names))]
    public async Task SubnormalBoundariesAndUnrepresentableIntermediateBodies(string name)
    {
        var baseline =
            name == "RickshawMan"
                ? KickingRickshawComparison.RickshawBars(0)
                : KickingRickshawComparison.KickingBars(0);
        var unit = 8 * double.Epsilon;
        var tiny = baseline
            .Select((b, i) => Make(i, b.Open * unit, b.High * unit, b.Low * unit, b.Close * unit))
            .ToArray();
        Assert.Equal(
            name == "KickingByLength" ? -100 : 100,
            (
                await Run(
                    KickingRickshawComparison.Create(name, 10, name == "RickshawMan" ? 5 : 10),
                    tiny
                )
            )[^1]
        );
        var m = double.MaxValue;
        var bars = Enumerable.Range(0, 10).Select(i => Make(i, 0, m, -m, 0)).ToList();
        if (name == "RickshawMan")
            bars.Add(Make(10, m * .75, m, m / 2, m * .75));
        else
        {
            bars.Add(Make(10, m / 4, m / 4, -m, -m));
            bars.Add(Make(11, m / 2, m, m / 2, m));
        }
        Assert.Equal(
            name == "KickingByLength" ? -100 : 100,
            (
                await Run(
                    KickingRickshawComparison.Create(name, 10, name == "RickshawMan" ? 5 : 10),
                    bars.ToArray()
                )
            )[^1]
        );
    }

    [Theory, MemberData(nameof(Names))]
    public async Task LazyStorageAndPublicPeriodBounds(string name)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            KickingRickshawComparison.Create(name, 0, 1)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            KickingRickshawComparison.Create(name, 1, 0)
        );
        var max = name == "RickshawMan" ? int.MaxValue : int.MaxValue - 1;
        if (name != "RickshawMan")
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                KickingRickshawComparison.Create(name, int.MaxValue, 1)
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                KickingRickshawComparison.Create(name, 1, int.MaxValue)
            );
        }
        Assert.Equal(
            new double[1],
            await Run(KickingRickshawComparison.Create(name, max, 1), [Make(0, 4, 10, 0, 6)])
        );
        Assert.Equal(
            new double[1],
            await Run(KickingRickshawComparison.Create(name, 1, max), [Make(0, 4, 10, 0, 6)])
        );
    }

    [Fact]
    public void DefaultLookbacksAndShadowSettings()
    {
        Assert.Equal(11, TALib.Candles.KickingLookback());
        Assert.Equal(11, TALib.Candles.KickingByLengthLookback());
        Assert.Equal(10, TALib.Candles.RickshawManLookback());
        var longShadow = TALib.Core.CandleSettings.Get(TALib.Core.CandleSettingType.ShadowLong);
        Assert.Equal(0, longShadow.AveragePeriod);
        Assert.Equal(1, longShadow.Factor);
        var shortShadow = TALib.Core.CandleSettings.Get(
            TALib.Core.CandleSettingType.ShadowVeryShort
        );
        Assert.Equal(10, shortShadow.AveragePeriod);
        Assert.Equal(.1, shortShadow.Factor);
        var near = TALib.Core.CandleSettings.Get(TALib.Core.CandleSettingType.Near);
        Assert.Equal(5, near.AveragePeriod);
        Assert.Equal(.2, near.Factor);
    }

    [Fact]
    public void AddedFixtureAcrossExistingCandlePairs()
    {
        foreach (var pair in ComparisonPairs.All.Where(p => p.IsCandle))
            ComparisonVerifier.Check(pair, KickingRickshawComparison.Fixture(), 20);
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

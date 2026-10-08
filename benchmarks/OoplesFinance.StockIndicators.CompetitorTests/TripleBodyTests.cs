using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class TripleBodyTests
{
    public static IEnumerable<object[]> Cases =>
        Enumerable.Range(0, TripleBodyComparison.Cases.Length).Select(i => new object[] { i });
    public static IEnumerable<object[]> Names =>
        TripleBodyComparison.Names.Select(n => new object[] { n });

    [Theory, MemberData(nameof(Cases))]
    public async Task HandCalculatedColorsGapsContainmentAndShortBodyBoundaries(int index)
    {
        var c = TripleBodyComparison.Cases[index];
        var bars = TripleBodyComparison.BarsFor(c);
        var actual = await Run(TripleBodyComparison.Create(c.Name), bars);
        Assert.All(actual.Take(12), v => Assert.Equal(0, v));
        Assert.Equal(c.Expected, actual[^1]);
        var data = CompetitorData.FromOhlc(
            bars.Select(b => b.Open).ToArray(),
            bars.Select(b => b.High).ToArray(),
            bars.Select(b => b.Low).ToArray(),
            bars.Select(b => b.Close).ToArray()
        );
        var pair = ComparisonPairs.Get("TaLib.Candles." + c.Name);
        Assert.Equal(c.Expected, pair.Competitor(data, 20).Outputs["Value"].Values[^1]);
        Assert.Equal(c.Expected, pair.Reference!(data, 20).Outputs["Value"].Values[^1]);
    }

    [Theory, MemberData(nameof(Names))]
    public async Task IndependentContractsIncludeUnequalWindowsAndLifecycle(string name)
    {
        foreach (var (longPeriod, shortPeriod) in new[] { (1, 1), (1, 3), (3, 1), (10, 10) })
        {
            var indicator = TripleBodyComparison.Create(name, longPeriod, shortPeriod);
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    indicator.GetType(),
                    "windows",
                    () => TripleBodyComparison.Create(name, longPeriod, shortPeriod)
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
    }

    [Theory, MemberData(nameof(Names))]
    public async Task LongBodyEqualityRejectsAnOtherwiseValidPattern(string name)
    {
        var c = TripleBodyComparison.Cases.First(c => c.Name == name && c.Expected != 0);
        var bars = TripleBodyComparison.BarsFor(c);
        Assert.Equal(c.Expected, (await Run(TripleBodyComparison.Create(name), bars))[^1]);
        for (var i = 0; i < 10; i++)
            bars[i] = Make(i, 2, 30, 0, 9);
        Assert.Equal(0, (await Run(TripleBodyComparison.Create(name), bars))[^1]);
    }

    [Theory, MemberData(nameof(Names))]
    public async Task BinaryScalingPreservesSubnormalSignals(string name)
    {
        var c = TripleBodyComparison.Cases.First(c => c.Name == name && c.Expected != 0);
        var unit = 8 * double.Epsilon;
        var bars = TripleBodyComparison
            .BarsFor(c)
            .Select((b, i) => Make(i, b.Open * unit, b.High * unit, b.Low * unit, b.Close * unit))
            .ToArray();
        Assert.Equal(c.Expected, (await Run(TripleBodyComparison.Create(name), bars))[^1]);
    }

    [Theory, MemberData(nameof(Names))]
    public async Task UnrepresentableBodiesStillClassify(string name)
    {
        var m = double.MaxValue;
        Bar[] tail = name switch
        {
            "ThreeInside" =>
            [
                Make(1, m / 2, m, -m, -m),
                Make(2, 0, m, -m, 1),
                Make(3, 0, m, -m, m * .75),
            ],
            "TwoCrows" =>
            [
                Make(1, -m, m, -m, m / 2),
                Make(2, m, m, -m, m * .75),
                Make(3, m * .875, m, -m, 0),
            ],
            "UpsideGapTwoCrows" =>
            [
                Make(1, -m, m, -m, m / 2),
                Make(2, m * .875, m, -m, m * .75),
                Make(3, m, m, -m, m * .625),
            ],
            _ =>
            [
                Make(1, m, m, -m / 2, -m / 2),
                Make(2, 0, m, -m, -m / 4),
                Make(3, -m / 2, m, -m, -m / 2),
            ],
        };
        var bars = new[] { Make(0, 0, m, -m, 0) }.Concat(tail).ToArray();
        Assert.Equal(
            name is "ThreeInside" or "UniqueThreeRiver" ? 100 : -100,
            (await Run(TripleBodyComparison.Create(name, 1, 1), bars))[^1]
        );
    }

    [Theory, MemberData(nameof(Names))]
    public async Task ValidatesPeriodsAndAllocatesHistoryLazily(string name)
    {
        foreach (var period in new[] { -1, 0, int.MaxValue - 1, int.MaxValue })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                TripleBodyComparison.Create(name, period, 1)
            );
            if (name != "TwoCrows")
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    TripleBodyComparison.Create(name, 1, period)
                );
        }
        var bars = new[] { Make(0, 4, 10, 0, 6) };
        Assert.Equal(
            new double[1],
            await Run(TripleBodyComparison.Create(name, int.MaxValue - 2, 1), bars)
        );
        if (name != "TwoCrows")
            Assert.Equal(
                new double[1],
                await Run(TripleBodyComparison.Create(name, 1, int.MaxValue - 2), bars)
            );
    }

    [Fact]
    public void LookbacksAndDefaultThresholds()
    {
        Assert.Equal(
            new[] { 12, 12, 12, 12 },
            new[]
            {
                TALib.Candles.ThreeInsideLookback(),
                TALib.Candles.TwoCrowsLookback(),
                TALib.Candles.UpsideGapTwoCrowsLookback(),
                TALib.Candles.UniqueThreeRiverLookback(),
            }
        );
        foreach (
            var kind in new[]
            {
                TALib.Core.CandleSettingType.BodyLong,
                TALib.Core.CandleSettingType.BodyShort,
            }
        )
        {
            Assert.Equal(10, TALib.Core.CandleSettings.Get(kind).AveragePeriod);
            Assert.Equal(1, TALib.Core.CandleSettings.Get(kind).Factor);
        }
    }

    [Fact]
    public void AddedFixtureAcrossExistingCandlePairs()
    {
        foreach (var pair in ComparisonPairs.All.Where(p => p.IsCandle))
            ComparisonVerifier.Check(pair, TripleBodyComparison.Fixture(), 20);
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

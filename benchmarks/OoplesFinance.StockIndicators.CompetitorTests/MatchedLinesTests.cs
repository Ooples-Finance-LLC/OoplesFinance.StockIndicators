using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class MatchedLinesTests
{
    public static IEnumerable<object[]> Cases =>
        Enumerable.Range(0, MatchedLinesComparison.Cases.Length).Select(i => new object[] { i });
    public static IEnumerable<object[]> Names =>
        MatchedLinesComparison.Names.Select(n => new object[] { n });

    [Theory, MemberData(nameof(Cases))]
    public async Task HandCalculatedToleranceEndpointsWindowOffsetsAndColors(int index)
    {
        var c = MatchedLinesComparison.Cases[index];
        foreach (
            var mirror in c.Name == "GapSideBySideWhiteLines"
                ? new[] { false }
                : new[] { false, true }
        )
        {
            var bars = mirror
                ? c
                    .Bars.Select(b => new Bar(b.Time, -b.Open, -b.Low, -b.High, -b.Close, 1))
                    .ToArray()
                : c.Bars;
            var expected = mirror ? -c.Expected : c.Expected;
            var actual = await Run(
                MatchedLinesComparison.Create(c.Name, c.Name == "GapSideBySideWhiteLines" ? 5 : 10),
                bars
            );
            Assert.Equal(expected, actual[^1]);
            Assert.All(actual.Take(actual.Length - 1), v => Assert.Equal(0, v));
            var data = MatchedLinesComparison.FromBars(bars);
            var pair = ComparisonPairs.Get("TaLib.Candles." + c.Name);
            Assert.Equal(expected, pair.Competitor(data, 20).Outputs["Value"].Values[^1]);
            Assert.Equal(expected, pair.Reference!(data, 20).Outputs["Value"].Values[^1]);
        }
    }

    [Theory, MemberData(nameof(Names))]
    public async Task IndependentReferencesUnequalWindowsAndLifecycle(string name)
    {
        foreach (var (a, b, c) in new[] { (1, 1, 1), (1, 3, 2), (3, 1, 4), (10, 5, 10) })
        {
            var indicator = MatchedLinesComparison.Create(name, a, b, c);
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    indicator.GetType(),
                    "windows",
                    () => MatchedLinesComparison.Create(name, a, b, c)
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
    }

    [Theory, MemberData(nameof(Names))]
    public async Task SubnormalScalingPreservesBoundarySignals(string name)
    {
        var c = MatchedLinesComparison.Cases.First(c => c.Name == name && c.Expected != 0);
        var u = 8 * double.Epsilon;
        var bars = c
            .Bars.Select((b, i) => Make(i, b.Open * u, b.High * u, b.Low * u, b.Close * u))
            .ToArray();
        Assert.Equal(
            c.Expected,
            (
                await Run(
                    MatchedLinesComparison.Create(name, name == "GapSideBySideWhiteLines" ? 5 : 10),
                    bars
                )
            )[^1]
        );
    }

    [Theory, MemberData(nameof(Names))]
    public async Task UnrepresentableBodiesAndRangesRemainFiniteSignals(string name)
    {
        var m = double.MaxValue;
        var gap = name == "GapSideBySideWhiteLines";
        var n = gap ? 5 : 10;
        var bars = Enumerable.Range(0, n).Select(i => Make(i, 0, m, -m, 0)).ToList();
        if (gap)
        {
            bars.Add(Make(n, -m, m, -m, -m * .75));
            bars.Add(Make(n + 1, -m / 2, m, -m, m));
            bars.Add(Make(n + 2, -m / 2, m, -m, m));
        }
        else if (name == "Counterattack")
        {
            bars.Add(Make(n, m, m, -m, -m / 2));
            bars.Add(Make(n + 1, -m, m, -m, -m / 2));
        }
        else
        {
            bars.Add(Make(n, -m / 2, m, -m, -m));
            bars.Add(Make(n + 1, -m / 2, m, -m / 2, m));
        }
        Assert.Equal(
            100,
            (await Run(MatchedLinesComparison.Create(name, gap ? 5 : 10), bars.ToArray()))[^1]
        );
    }

    [Theory, MemberData(nameof(Names))]
    public async Task ParameterValidationAndLazyStorage(string name)
    {
        var limit = int.MaxValue - (name == "GapSideBySideWhiteLines" ? 2 : 1);
        foreach (var invalid in new[] { 0, -1, limit + 1 })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                MatchedLinesComparison.Create(name, invalid, 1, 1)
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                MatchedLinesComparison.Create(name, 1, invalid, 1)
            );
            if (name == "SeparatingLines")
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    MatchedLinesComparison.Create(name, 1, 1, invalid)
                );
        }
        var bars = new[] { Make(0, 4, 10, 0, 6) };
        Assert.Equal(
            new double[1],
            await Run(MatchedLinesComparison.Create(name, limit, 1, 1), bars)
        );
        Assert.Equal(
            new double[1],
            await Run(MatchedLinesComparison.Create(name, 1, limit, 1), bars)
        );
        if (name == "SeparatingLines")
            Assert.Equal(
                new double[1],
                await Run(MatchedLinesComparison.Create(name, 1, 1, limit), bars)
            );
    }

    [Fact]
    public void PinnedLookbacksAndToleranceSettings()
    {
        Assert.Equal(11, TALib.Candles.CounterattackLookback());
        Assert.Equal(11, TALib.Candles.SeparatingLinesLookback());
        Assert.Equal(7, TALib.Candles.GapSideBySideWhiteLinesLookback());
        Assert.Equal(.05, TALib.Core.CandleSettings.Get(TALib.Core.CandleSettingType.Equal).Factor);
        Assert.Equal(.2, TALib.Core.CandleSettings.Get(TALib.Core.CandleSettingType.Near).Factor);
    }

    [Fact]
    public void AddedFixtureAcrossExistingCandlePairs()
    {
        foreach (var pair in ComparisonPairs.All.Where(p => p.IsCandle))
            ComparisonVerifier.Check(pair, MatchedLinesComparison.Fixture(), 20);
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

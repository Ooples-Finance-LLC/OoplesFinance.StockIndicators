using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class PenetrationCandleTests
{
    [Theory]
    [InlineData("PiercingLine")]
    [InlineData("DarkCloudCover")]
    public void GoldenGapMidpointAndInsideBodyBoundaries(string name)
    {
        var data = PenetrationCandleComparison.Fixture();
        var pair = PenetrationCandleComparison.Pair(name);
        var result = pair.Competitor(data, 20).Outputs["Value"].Values;
        for (var i = 0; i < PenetrationCandleComparison.Candidates.Length; i++)
        {
            var matches = i is 0 or 3 or 5 or 8;
            var offset = name == "PiercingLine" ? 11 : 23;
            Assert.Equal(
                matches
                    ? name == "PiercingLine"
                        ? 100
                        : -100
                    : 0,
                result[i * 24 + offset]
            );
            Assert.Equal(0, result[i * 24 + (offset == 11 ? 23 : 11)]);
        }
        ComparisonVerifier.Check(pair, data, 20);
    }

    [Theory]
    [InlineData("PiercingLine")]
    [InlineData("DarkCloudCover")]
    public async Task IndependentContractsLifecycleAndParameters(string name)
    {
        foreach (var period in new[] { 1, 3, 10 })
        foreach (
            var penetration in name == "PiercingLine" ? new[] { .5 } : new[] { 0d, .25, .5, 1, 2 }
        )
        {
            var indicator = PenetrationCandleComparison.Create(name, period, penetration);
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    indicator.GetType(),
                    "threshold",
                    () => PenetrationCandleComparison.Create(name, period, penetration)
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
    }

    [Fact]
    public void PenetrationSettingsAndLookbacks()
    {
        foreach (var penetration in new[] { 0d, .25, .5, .75, 1, 2 })
            ComparisonVerifier.Check(
                PenetrationCandleComparison.Pair("DarkCloudCover", penetration),
                PenetrationCandleComparison.Fixture(),
                20
            );
        Assert.Equal(11, TALib.Candles.PiercingLineLookback());
        Assert.Equal(11, TALib.Candles.DarkCloudCoverLookback());
        foreach (
            var value in new[] { -1d, double.NaN, double.NegativeInfinity, double.PositiveInfinity }
        )
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new DarkCloudCoverCandle(penetration: value)
            );
    }

    [Theory]
    [InlineData("PiercingLine")]
    [InlineData("DarkCloudCover")]
    public async Task LongBodyThresholdsAreStrict(string name)
    {
        // The anchor's body exactly equals the preceding mean: not long.
        var mirror = name == "DarkCloudCover";
        var bars = Enumerable
            .Range(0, 10)
            .Select(i => Make(i, 8, 10, 0, 0, mirror))
            .Append(Make(10, 8, 10, 0, 0, mirror))
            .Append(Make(11, -1, 6, -2, 5, mirror))
            .ToArray();
        Assert.Equal(0, (await Run(PenetrationCandleComparison.Create(name), bars))[^1]);
        // Current body is not long: rejected only by Piercing, which requires two long candles.
        bars = Enumerable
            .Range(0, 10)
            .Select(i => Make(i, 5, 10, 0, 0, mirror))
            .Append(Make(10, 8, 10, 0, 0, mirror))
            .Append(Make(11, -.125, 5, -1, 4.125, mirror))
            .ToArray();
        Assert.Equal(
            mirror ? -100 : 0,
            (await Run(PenetrationCandleComparison.Create(name), bars))[^1]
        );
    }

    [Theory]
    [InlineData("PiercingLine")]
    [InlineData("DarkCloudCover")]
    public async Task SubnormalScalingAndExtremeThresholdArithmetic(string name)
    {
        var mirror = name == "DarkCloudCover";
        var u = double.Epsilon;
        var bars = Enumerable
            .Range(0, 10)
            .Select(i => Make(i, 4 * u, 10 * u, 0, 6 * u, mirror))
            .Append(Make(10, 8 * u, 10 * u, 0, 0, mirror))
            .Append(Make(11, -u, 6 * u, -2 * u, 5 * u, mirror))
            .ToArray();
        Assert.Equal(
            mirror ? -100 : 100,
            (await Run(PenetrationCandleComparison.Create(name), bars))[^1]
        );
        // Piercing's two bodies exceed MaxValue; no intermediate body subtraction may overflow.
        var m = double.MaxValue;
        bars =
        [
            Make(0, 0, m, -m, 0, mirror),
            Make(1, m, m, -m / 2, -m / 2, mirror),
            Make(2, -m, m, -m, m * .75, mirror),
        ];
        Assert.Equal(
            mirror ? -100 : 100,
            (await Run(PenetrationCandleComparison.Create(name, 1), bars))[^1]
        );
        if (mirror)
            Assert.Equal(0, (await Run(new DarkCloudCoverCandle(1, double.MaxValue), bars))[^1]);
    }

    [Theory]
    [InlineData("PiercingLine")]
    [InlineData("DarkCloudCover")]
    public async Task LazyStorageAndInvalidPeriods(string name)
    {
        foreach (var period in new[] { -1, 0, int.MaxValue })
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                PenetrationCandleComparison.Create(name, period)
            );
        Assert.Equal(
            new double[1],
            await Run(
                PenetrationCandleComparison.Create(name, int.MaxValue - 1),
                [Make(0, 0, 1, 0, 1, false)]
            )
        );
    }

    [Fact]
    public void AddedFixtureAgreesForExistingCandlePairs()
    {
        foreach (var pair in ComparisonPairs.All.Where(p => p.IsCandle))
            ComparisonVerifier.Check(pair, PenetrationCandleComparison.Fixture(), 20);
    }

    private static Bar Make(int i, double o, double h, double l, double c, bool mirror) =>
        mirror
            ? new(DateTime.UnixEpoch.AddDays(i), -o, -l, -h, -c, 1)
            : new(DateTime.UnixEpoch.AddDays(i), o, h, l, c, 1);

    private static async Task<double[]> Run(IIndicator indicator, Bar[] bars)
    {
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        return run[indicator.Outputs[0]].ToArray();
    }
}

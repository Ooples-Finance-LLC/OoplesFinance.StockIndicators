using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;
using TC = Trady.Analysis.Candlestick;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class DelayedDarkCloudTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 3)]
    [InlineData(5, 2)]
    public void GoldenDelayedSignalsDoNotRequireDowntrendOrLongBodies(int period, int delay)
    {
        var data = DelayedDarkCloudComparison.Fixture(period, delay);
        var pair = DelayedDarkCloudComparison.Create(delay);
        var actual = pair.Competitor(data, period).Outputs["Value"].Values;
        var block = period + delay + 1;
        for (var i = 0; i < DelayedDarkCloudComparison.Candidates.Length; i++)
            Assert.Equal(i is 0 or 3 or 4 ? 1 : 0, actual[(i + 1) * block - 1]);
        Assert.All(actual.Take(delay), v => Assert.True(double.IsNaN(v)));
        ComparisonVerifier.Check(pair, data, period);
        foreach (var longPeriod in new[] { 1, 20, 40 })
        foreach (var threshold in new[] { 0m, .75m, 1m })
            ComparisonVerifier.Compare(
                pair.Competitor(data, period),
                DelayedDarkCloudComparison
                    .Create(delay, longPeriod, threshold)
                    .Competitor(data, period),
                "ignored long-body arguments"
            );
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 3)]
    [InlineData(5, 2)]
    public async Task IndependentReferenceAndLifecycle(int period, int delay)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(DelayedDarkCloudCoverPattern),
                "delay",
                () => new DelayedDarkCloudCoverPattern(period, delay)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void TupleGenericRoutesAndZeroDelayFailure()
    {
        var data = DelayedDarkCloudComparison.Fixture();
        var input = data.Candles.Select(c => (c.Open, c.High, c.Low, c.Close)).ToArray();
        foreach (var period in new[] { 1, 3, 5 })
        foreach (var delay in new[] { 1, 2, 3 })
        {
            var expected = DelayedDarkCloudComparison
                .Create(delay)
                .Competitor(data, period)
                .Outputs["Value"]
                .Values;
            var tuple = new TC.DarkCloudCoverByTuple(input, period, delay).Compute();
            var generic = new TC.DarkCloudCover<(decimal, decimal, decimal, decimal), bool?>(
                input,
                x => x,
                period,
                delay
            ).Compute();
            Assert.Equal(
                expected,
                tuple.Select(v =>
                    v.HasValue
                        ? v.Value
                            ? 1d
                            : 0
                        : double.NaN
                )
            );
            Assert.Equal(
                expected,
                generic.Select(v =>
                    v.HasValue
                        ? v.Value
                            ? 1d
                            : 0
                        : double.NaN
                )
            );
        }
        var error = Record.Exception(() => new TC.DarkCloudCoverByTuple(input, 3, 0).Compute());
        Assert.NotNull(error);
        Assert.Contains(
            error.GetType(),
            new[] { typeof(IndexOutOfRangeException), typeof(ArgumentOutOfRangeException) }
        );
    }

    [Fact]
    public async Task MidpointEqualityTinyPricesAndOverflowAreExact()
    {
        var m = double.MaxValue;
        var bars = new[]
        {
            Make(0, m / 4, m / 2, 0, m / 2),
            Make(1, m / 2, m, m / 4, m * .75),
            Make(2, m, m, m / 4, m / 2),
        };
        Assert.Equal(1, (await Run(new DelayedDarkCloudCoverPattern(1, 1), bars))[2]);
        var u = double.Epsilon;
        bars =
        [
            Make(0, 0, 8 * u, -2 * u, 4 * u),
            Make(1, 4 * u, 10 * u, 0, 8 * u),
            Make(2, 9 * u, 10 * u, 0, 6 * u),
        ];
        Assert.Equal(0, (await Run(new DelayedDarkCloudCoverPattern(1, 1), bars))[2]);
        bars[2] = Make(2, 9 * u, 10 * u, 0, 5 * u);
        Assert.Equal(1, (await Run(new DelayedDarkCloudCoverPattern(1, 1), bars))[2]);
        bars[0] = Make(0, 0, 10 * u, -2 * u, 4 * u); // equal highs break the uptrend
        Assert.Equal(0, (await Run(new DelayedDarkCloudCoverPattern(1, 1), bars))[2]);
        bars[0] = Make(0, 0, 8 * u, 0, 4 * u); // equal lows also break it
        Assert.Equal(0, (await Run(new DelayedDarkCloudCoverPattern(1, 1), bars))[2]);
    }

    [Fact]
    public async Task PeriodsValidateAndLargeDelaysAllocateLazily()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DelayedDarkCloudCoverPattern(0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DelayedDarkCloudCoverPattern(delay: 0)
        );
        var bars = DelayedDarkCloudComparison.Fixture().IndicatorBars;
        Assert.Equal(
            new double[bars.Length],
            await Run(new DelayedDarkCloudCoverPattern(int.MaxValue, 1), bars)
        );
        Assert.Equal(
            new double[bars.Length],
            await Run(new DelayedDarkCloudCoverPattern(1, int.MaxValue), bars)
        );
    }

    [Fact]
    public void AddedFixtureAcrossExistingCandlePairs()
    {
        foreach (var pair in ComparisonPairs.All.Where(p => p.IsCandle))
            ComparisonVerifier.Check(pair, DelayedDarkCloudComparison.Fixture(), 3);
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

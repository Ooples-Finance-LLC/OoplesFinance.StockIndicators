using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Trady.Analysis;
using Trady.Core.Infrastructure;
using Xunit;
using T = Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class PercentileComparisonTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryRankBoundaryAndStartupMatchesIndependentReferences(bool quan)
    {
        foreach (var fraction in new[] { 0d, .1, .25, .5, .9, 1 })
        foreach (var period in new[] { 2, 3, 4, 20 })
            ComparisonVerifier.Check(
                PercentileComparison.Create(quan, false, fraction),
                PercentileComparison.Fixture(),
                period
            );
        foreach (var period in new[] { 1, 2, 3, 4, 20 })
            ComparisonVerifier.Check(
                PercentileComparison.Create(quan, true),
                PercentileComparison.Fixture(),
                period
            );
    }

    [Theory]
    [InlineData(0d, false)]
    [InlineData(.1, false)]
    [InlineData(.5, false)]
    [InlineData(1d, false)]
    [InlineData(.25, true)]
    [InlineData(.5, true)]
    public async Task IndependentContractCoversLifecycleAndExtremeInputs(
        double fraction,
        bool average
    )
    {
        foreach (var period in new[] { 1, 2, 3, 20 })
        {
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    typeof(RollingPercentile),
                    "rolling quantile",
                    () => new RollingPercentile(period, fraction, average)
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LargePeriodsAreLazyAndConvexInterpolationStaysFinite(bool average)
    {
        var indicator = new RollingPercentile(int.MaxValue, .5, average);
        var bars = new[] { double.MaxValue, double.MaxValue, -double.MaxValue }
            .Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 0))
            .ToArray();
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        var values = run[indicator.Outputs[0]].ToArray();
        Assert.Equal(double.MaxValue, values[0]);
        Assert.Equal(double.MaxValue, values[1]);
        Assert.Equal(average ? double.MaxValue / 3 : double.MaxValue, values[2]);
    }

    [Fact]
    public async Task SubnormalHalfwayRoundsToEvenAndOppositeExtremesCancel()
    {
        foreach (
            var (input, expected) in new[]
            {
                (new[] { double.Epsilon, 2 * double.Epsilon }, 2 * double.Epsilon),
                (new[] { -double.MaxValue, double.MaxValue }, 0d),
                (new[] { 9d, 1d, 5d, 5d, 20d }, 5d),
            }
        )
        {
            var indicator = new RollingPercentile(2);
            // Last fixture specifically checks duplicate insertion and expiration.
            if (input.Length > 2)
                indicator = new RollingPercentile(3);
            var bars = input
                .Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 0))
                .ToArray();
            using var run = await new StockIndicatorBuilder()
                .ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(indicator)
                .BuildAsync();
            Assert.Equal(expected, run[indicator.Outputs[0]].ToArray()[^1]);
        }
    }

    [Fact]
    public void TradyObjectGenericAndTupleRoutesAgree()
    {
        var data = PercentileComparison.Fixture();
        foreach (var period in new[] { 1, 2, 3, 20 })
        foreach (var percent in new[] { 0m, .1m, .5m, 1m })
        {
            var expected = new T.Percentile(data.Candles, period, percent)
                .Compute()
                .Select(v => v.Tick)
                .ToArray();
            Assert.Equal(
                expected,
                new T.Percentile<IOhlcv, AnalyzableTick<decimal?>>(
                    data.Candles,
                    c => c.Close,
                    period,
                    percent
                )
                    .Compute()
                    .Select(v => v.Tick)
            );
            Assert.Equal(
                expected,
                new T.PercentileByTuple(
                    data.Candles.Select(c => c.Close),
                    period,
                    percent
                ).Compute()
            );
            Assert.Equal(
                expected,
                new T.PercentileByTuple(
                    data.Candles.Select(c => (decimal?)c.Close),
                    period,
                    percent
                ).Compute()
            );
        }
        var median = new T.Median(data.Candles, 3).Compute().Select(v => v.Tick).ToArray();
        Assert.Equal(median, new T.MedianByTuple(data.Candles.Select(c => c.Close), 3).Compute());
        Assert.Equal(
            median,
            new T.MedianByTuple(data.Candles.Select(c => (decimal?)c.Close), 3).Compute()
        );
        Assert.Equal(
            median,
            new T.Median<IOhlcv, AnalyzableTick<decimal?>>(data.Candles, c => c.Close, 3)
                .Compute()
                .Select(v => v.Tick)
        );
    }

    [Fact]
    public void ConstructorRejectsUndefinedRanksAndPeriods()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RollingPercentile(0));
        foreach (
            var fraction in new[]
            {
                -.1,
                1.1,
                double.NaN,
                double.NegativeInfinity,
                double.PositiveInfinity,
            }
        )
            Assert.Throws<ArgumentOutOfRangeException>(() => new RollingPercentile(2, fraction));
        Assert.Throws<ArgumentOutOfRangeException>(() => new QuanTAlib.Percentile(1, 50));
    }

    [Fact]
    public void CorruptionInEitherArmIsDetected()
    {
        static ComparisonSeries Corrupt(ComparisonSeries series) =>
            new(
                series.Outputs.ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value with { Values = kv.Value.Values.Select(v => v + 1).ToArray() }
                )
            );
        foreach (var pair in PercentileComparison.Pairs)
        {
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    pair with
                    {
                        Library = (d, p) => Corrupt(pair.Ooples(d, p)),
                    },
                    PercentileComparison.Fixture(),
                    3
                )
            );
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    pair with
                    {
                        Competitor = (d, p) => Corrupt(pair.Competitor(d, p)),
                    },
                    PercentileComparison.Fixture(),
                    3
                )
            );
        }
    }
}

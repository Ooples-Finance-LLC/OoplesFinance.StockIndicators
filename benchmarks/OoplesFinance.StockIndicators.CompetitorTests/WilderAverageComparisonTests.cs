using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Trady.Analysis;
using Trady.Core.Infrastructure;
using Xunit;
using T = Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class WilderAverageComparisonTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BothSeedContractsVerifyIndependentFormulaAndLifecycle(bool seedWithAverage)
    {
        foreach (var period in new[] { 1, 2, 3, 20 })
        {
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    typeof(WilderMovingAverage),
                    "Wilder seed",
                    () => new WilderMovingAverage(period, seedWithAverage)
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
    }

    [Fact]
    public async Task HandCalculatedSeedsAndFirstRecurrenceAreDistinct()
    {
        var meanSeed = new WilderMovingAverage(3);
        var firstSeed = new WilderMovingAverage(3, false);
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(WilderAverageComparison.Fixture().IndicatorBars))
            .ConfigureIndicators(meanSeed, firstSeed)
            .BuildAsync();
        Assert.Equal(new[] { 3d, 7.5, 3d, 5d }, run[meanSeed.Outputs[0]].ToArray().Take(4));
        Assert.Equal(new[] { 3d, 6d, 2d, 13d / 3 }, run[firstSeed.Outputs[0]].ToArray().Take(4));
        Assert.Equal(2, meanSeed.WarmupBars);
        Assert.Equal(0, firstSeed.WarmupBars);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConvexRecurrencePreservesExtremeAndSubnormalValues(bool meanSeed)
    {
        foreach (
            var input in new[]
            {
                new[] { double.MaxValue, double.MaxValue, double.MaxValue },
                new[] { -double.MaxValue, -double.MaxValue, -double.MaxValue },
                new[] { double.Epsilon, double.Epsilon, double.Epsilon },
            }
        )
        {
            var bars = input
                .Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 0))
                .ToArray();
            var indicator = new WilderMovingAverage(2, meanSeed);
            using var run = await new StockIndicatorBuilder()
                .ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(indicator)
                .BuildAsync();
            Assert.Equal(input, run[indicator.Outputs[0]].ToArray());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MaximumPeriodNeedsNoEagerStorageOrPeriodArithmeticOverflow(bool meanSeed)
    {
        var indicator = new WilderMovingAverage(int.MaxValue, meanSeed);
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(WilderAverageComparison.Fixture().IndicatorBars.Take(2)))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        var result = run[indicator.Outputs[0]].ToArray();
        Assert.Equal(3, result[0]);
        Assert.Equal(meanSeed ? 7.5 : 3 + 9d / int.MaxValue, result[1]);
    }

    [Fact]
    public void PackageRoutesAndWindowBoundariesAreCovered()
    {
        var data = WilderAverageComparison.Fixture();
        foreach (var pair in WilderAverageComparison.Pairs)
        foreach (var period in new[] { 1, 2, 3, 20 })
        {
            ComparisonVerifier.Check(pair, data, period);
            ComparisonVerifier.Check(pair, CompetitorData.FromCloses([5]), period);
        }
        foreach (var period in new[] { 1, 3, 20 })
        {
            var expected = new T.ModifiedMovingAverage(data.Candles, period)
                .Compute()
                .Select(v => v.Tick)
                .ToArray();
            Assert.Equal(
                expected,
                new T.ModifiedMovingAverageByTuple(
                    data.Candles.Select(c => c.Close),
                    period
                ).Compute()
            );
            Assert.Equal(
                expected,
                new T.ModifiedMovingAverageByTuple(
                    data.Candles.Select(c => (decimal?)c.Close),
                    period
                ).Compute()
            );
            Assert.Equal(
                expected,
                new T.ModifiedMovingAverage<IOhlcv, AnalyzableTick<decimal?>>(
                    data.Candles,
                    c => c.Close,
                    period
                )
                    .Compute()
                    .Select(v => v.Tick)
            );
        }
    }

    [Fact]
    public void WrongSeedOrCoefficientAndCorruptedCompetitorAreDetected()
    {
        var data = WilderAverageComparison.Fixture();
        var expected = WilderAverageComparison.Pairs.Single(p => p.Id == "Skender.GetSmma");
        var other = WilderAverageComparison.Pairs.Single(p =>
            p.Id.StartsWith("Trady.", StringComparison.Ordinal)
        );
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Check(
                expected with
                {
                    Library = (d, p) =>
                        new ComparisonSeries(p - 1, other.Ooples(d, p).Outputs["Value"].Values),
                },
                data,
                3
            )
        );
        foreach (var pair in WilderAverageComparison.Pairs)
        {
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    pair with
                    {
                        Library = (d, p) =>
                            new ComparisonSeries(
                                pair.Ooples(d, p).Outputs["Value"].FirstValid,
                                pair.Ooples(d, 1).Outputs["Value"].Values
                            ),
                    },
                    data,
                    3
                )
            );
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    pair with
                    {
                        Competitor = (d, p) =>
                            new ComparisonSeries(
                                pair.Competitor(d, p).Outputs["Value"].FirstValid,
                                pair.Competitor(d, p)
                                    .Outputs["Value"]
                                    .Values.Select(v => v + 1)
                                    .ToArray()
                            ),
                    },
                    data,
                    3
                )
            );
        }
    }

    [Fact]
    public void InvalidPeriodIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WilderMovingAverage(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WilderMovingAverage(-1));
    }
}

using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Trady.Analysis;
using Trady.Core.Infrastructure;
using Xunit;
using T = Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SeededAtrComparisonTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(20)]
    public async Task ScalarAndDetailedIndependentContractsCoverLifecycle(int period)
    {
        foreach (var details in new[] { false, true })
        {
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    details ? typeof(AverageTrueRangeWithDetails) : typeof(SeededAverageTrueRange),
                    "ATR seed",
                    () =>
                        details
                            ? new AverageTrueRangeWithDetails(period)
                            : new SeededAverageTrueRange(period)
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
    }

    [Fact]
    public async Task HandCheckedRangesSeedAndMissingPercentRecoverAfterZeroClose()
    {
        var data = CompetitorData.FromOhlc(
            [10, 14, 11, 0, -3],
            [12, 16, 13, 0, -1],
            [8, 12, 9, 0, -5],
            [10, 14, 11, 0, -3]
        );
        var indicator = new AverageTrueRangeWithDetails(2);
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        Assert.Equal(new[] { 0d, 6, 5, 11, 5 }, run[indicator.TrueRange].ToArray());
        Assert.Equal(new[] { 0d, 0, 5.5, 8.25, 6.625 }, run[indicator.Average].ToArray());
        Assert.Equal(new[] { 0d, 0, 1, 0, 1 }, run[indicator.PercentIsDefined].ToArray());
        Assert.Equal(50, run[indicator.Percent].ToArray()[2]);
        Assert.Equal(-662.5 / 3, run[indicator.Percent].ToArray()[4]);
        Assert.Same(indicator.Average, indicator.PrimaryOutput);
        foreach (var pair in SeededAtrComparison.Pairs)
            ComparisonVerifier.Check(pair, data, 2);
    }

    [Fact]
    public async Task OversizedUnpublishedRangeStillProducesFiniteAverage()
    {
        var bars = new[]
        {
            new Bar(DateTime.UnixEpoch, 0, 0, 0, 0, 0),
            new Bar(DateTime.UnixEpoch.AddDays(1), 0, double.MaxValue, -double.MaxValue, 0, 0),
            new Bar(DateTime.UnixEpoch.AddDays(2), 0, 0, 0, 0, 0),
        };
        var indicator = new SeededAverageTrueRange(2);
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        Assert.Equal(new[] { 0d, 0, double.MaxValue }, run[indicator.Outputs[0]].ToArray());
    }

    [Fact]
    public async Task SubnormalRangesAndMaximumPeriodAreSupportedWithoutAllocation()
    {
        var bars = Enumerable
            .Range(0, 4)
            .Select(i => new Bar(DateTime.UnixEpoch.AddDays(i), 0, double.Epsilon, 0, 0, 0))
            .ToArray();
        var small = new SeededAverageTrueRange(2);
        var large = new SeededAverageTrueRange(int.MaxValue);
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(small, large)
            .BuildAsync();
        Assert.Equal(
            new[] { 0d, 0, double.Epsilon, double.Epsilon },
            run[small.Outputs[0]].ToArray()
        );
        Assert.All(run[large.Outputs[0]].ToArray(), v => Assert.Equal(0, v));
    }

    [Fact]
    public void NativeBoundaryAndTradyGenericTupleRoutes()
    {
        var data = SeededAtrComparison.Fixture();
        foreach (var pair in SeededAtrComparison.Pairs)
        foreach (var period in new[] { 2, 3, 20 })
        {
            ComparisonVerifier.Check(pair, data, period);
            ComparisonVerifier.Check(pair, CompetitorData.FromCloses([5]), period);
        }
        foreach (var period in new[] { 1, 2, 3, 20 })
        {
            var expected = new T.AverageTrueRange(data.Candles, period)
                .Compute()
                .Select(r => r.Tick)
                .ToArray();
            Assert.Equal(
                expected,
                new T.AverageTrueRangeByTuple(
                    data.Candles.Select(c => (c.High, c.Low, c.Close)),
                    period
                ).Compute()
            );
            Assert.Equal(
                expected,
                new T.AverageTrueRange<IOhlcv, AnalyzableTick<decimal?>>(
                    data.Candles,
                    c => (c.High, c.Low, c.Close),
                    period
                )
                    .Compute()
                    .Select(r => r.Tick)
            );
        }
        foreach (var pair in SeededAtrComparison.Pairs.Where(p => p.Id != "Skender.GetAtr"))
            ComparisonVerifier.Check(pair, data, 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => data.Quotes.GetAtr(1).ToArray());
    }

    [Fact]
    public void EverySkenderOutputAndMissingFlagRejectsCorruption()
    {
        var pair = SeededAtrComparison.Pairs.Single(p => p.Id == "Skender.GetAtr");
        foreach (var name in new[] { "Tr", "Atr", "Atrp" })
        foreach (var native in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData data, int period)
            {
                var result = native ? pair.Competitor(data, period) : pair.Ooples(data, period);
                var output = result.Outputs[name];
                for (var i = output.FirstValid; i < output.Values.Length; i++)
                    if (output.Present![i])
                        output.Values[i] += 1;
                return result;
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    SeededAtrComparison.Fixture(),
                    2
                )
            );
        }
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Check(
                pair with
                {
                    Library = (d, p) =>
                    {
                        var result = pair.Ooples(d, p);
                        result.Outputs["Atrp"].Present![4] = true;
                        return result;
                    },
                },
                SeededAtrComparison.Fixture(),
                2
            )
        );
    }

    [Fact]
    public void InvalidPeriodIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SeededAverageTrueRange(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AverageTrueRangeWithDetails(0));
    }
}

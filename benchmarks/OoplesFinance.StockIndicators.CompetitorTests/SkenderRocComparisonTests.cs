using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SkenderRocComparisonTests
{
    private static CompetitorData Fixture() =>
        CompetitorData.FromOhlc(
            [1d, 2, 3, 4, 5, 6, 7, 8, 9, 10],
            [20d, 21, 22, 23, 24, 25, 26, 27, 28, 29],
            [-20d, -21, -22, -23, -24, -25, -26, -27, -28, -29],
            [0d, 2, 4, 0, -2, 0, -2, -4, -8, -8]
        );

    [Fact]
    public void AllOutputsMatchHandCalculatedMissingWindowsAndRecovery()
    {
        var expected = new ComparisonSeries(
            new Dictionary<string, ComparisonOutput>
            {
                ["Momentum"] = new(2, [double.NaN, double.NaN, 4, -2, -6, 0, 0, -4, -6, -4]),
                ["Roc"] = new(
                    2,
                    [
                        double.NaN,
                        double.NaN,
                        double.NaN,
                        -100,
                        -150,
                        double.NaN,
                        0,
                        double.NaN,
                        300,
                        100,
                    ],
                    [false, false, false, true, true, false, true, false, true, true]
                ),
                ["RocSma"] = new(
                    3,
                    [
                        double.NaN,
                        double.NaN,
                        double.NaN,
                        double.NaN,
                        -125,
                        double.NaN,
                        double.NaN,
                        double.NaN,
                        double.NaN,
                        200,
                    ],
                    [false, false, false, false, true, false, false, false, false, true]
                ),
            }
        );
        var pair = SkenderRocComparison.Create(2);
        var data = Fixture();
        ComparisonVerifier.Compare(expected, pair.Reference!(data, 2), "reference");
        ComparisonVerifier.Compare(expected, pair.Ooples(data, 2), "Ooples");
        ComparisonVerifier.Compare(expected, pair.Competitor(data, 2), "Skender");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public void OptionalAverageAndPeriodCombinationsPreserveAllOutputs(int? averagePeriod)
    {
        var pair = SkenderRocComparison.Create(averagePeriod);
        foreach (var period in new[] { 1, 2, 3, 20 })
        {
            ComparisonVerifier.Check(pair, Fixture(), period);
            ComparisonVerifier.Check(pair, ComparisonVerifier.Fixture("walk", 101), period);
        }
        if (averagePeriod is null)
        {
            var output = pair.Ooples(Fixture(), 2).Outputs["RocSma"];
            Assert.Equal(0, output.FirstValid);
            Assert.All(output.Present!, value => Assert.False(value));
        }
    }

    [Theory]
    [InlineData(1, null)]
    [InlineData(1, 1)]
    [InlineData(1, 3)]
    [InlineData(3, 2)]
    [InlineData(20, 5)]
    public async Task FiveExactContractsAndLifecycle(int period, int? averagePeriod)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(RateOfChangeWithAverage),
                "periods",
                () => new RateOfChangeWithAverage(period, averagePeriod)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public async Task ExtremeMeansAndLargePeriodsDoNotOverflowIntermediateState()
    {
        var indicator = new RateOfChangeWithAverage(1, 3);
        using var run = await Run(indicator, [1d, 1e306, 1, 1e306]);
        // Independent rational calculation of [100*(1e306-1), -100, 100*(1e306-1)] / 3.
        Assert.Equal(6.666666666666666e+307, run[indicator.Average][3]);
        Assert.Equal(1, run[indicator.AverageIsDefined][3]);
        foreach (
            var (period, averagePeriod) in new[] { (int.MaxValue, int.MaxValue), (1, int.MaxValue) }
        )
        {
            var large = new RateOfChangeWithAverage(period, averagePeriod);
            using var shortRun = await Run(large, [1d, 2, 3]);
            Assert.Equal(new double[3], shortRun[large.AverageIsDefined].ToArray());
        }
        await Assert.ThrowsAsync<IndicatorOutputException>(async () =>
        {
            using var rejected = await Run(new RateOfChangeWithAverage(1, 2), [double.Epsilon, 1]);
        });
    }

    [Fact]
    public void InvalidPeriodsAreRejected()
    {
        var indicator = new RateOfChangeWithAverage();
        Assert.Same(indicator.Roc, indicator.PrimaryOutput);
        Assert.Throws<ArgumentOutOfRangeException>(() => new RateOfChangeWithAverage(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RateOfChangeWithAverage(1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RateOfChangeWithAverage(1, -1));
    }

    private static Task<IIndicatorRun> Run(RateOfChangeWithAverage indicator, double[] values)
    {
        var bars = values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 1));
        return new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
    }
}

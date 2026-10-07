using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SkenderRocBandsComparisonTests
{
    [Fact]
    public void MissingSeedReturnDoesNotRestartTheEma()
    {
        var data = CompetitorData.FromCloses([0d, 1, 2, 4, 8]);
        var pair = SkenderRocBandsComparison.Create(2);
        ComparisonVerifier.Check(pair, data, 1);
        var result = pair.Ooples(data, 1);
        Assert.All(result.Outputs["RocEma"].Present!, present => Assert.False(present));
        Assert.Equal(100, result.Outputs["UpperBand"].Values[4]);
        Assert.True(result.Outputs["UpperBand"].Present![4]);
    }

    [Fact]
    public void MissingReturnPermanentlyInvalidatesEmaButBandsRecover()
    {
        var data = CompetitorData.FromOhlc(
            [1d, 1, 1, 1, 1, 1, 1],
            [40d, 41, 42, 43, 44, 45, 46],
            [-1d, -2, -3, -4, -5, -6, -7],
            [1d, 2, 4, 0, 8, 16, 32]
        );
        var expected = new ComparisonSeries(
            new Dictionary<string, ComparisonOutput>
            {
                ["Roc"] = Output(1, [null, 100, 100, -100, null, 100, 100]),
                ["RocEma"] = Output(2, [null, null, 100, -100d / 3, null, null, null]),
                ["UpperBand"] = Output(1, [null, 100, 100, 100, null, 100, 100]),
                ["LowerBand"] = Output(1, [null, -100, -100, -100, null, -100, -100]),
            }
        );
        var pair = SkenderRocBandsComparison.Create(2);
        ComparisonVerifier.Compare(expected, pair.Reference!(data, 1), "reference");
        ComparisonVerifier.Compare(expected, pair.Ooples(data, 1), "Ooples");
        ComparisonVerifier.Compare(expected, pair.Competitor(data, 1), "Skender");
    }

    [Fact]
    public void BandsAreRmsRatherThanCenteredDeviation()
    {
        var data = CompetitorData.FromCloses([1d, 1, 2, 4, 8]);
        var pair = SkenderRocBandsComparison.Create(2);
        foreach (
            var output in new[]
            {
                pair.Reference!(data, 2),
                pair.Ooples(data, 2),
                pair.Competitor(data, 2),
            }
        )
        {
            Assert.Equal(Math.Sqrt(50000), output.Outputs["UpperBand"].Values[3]);
            Assert.Equal(-Math.Sqrt(50000), output.Outputs["LowerBand"].Values[3]);
            Assert.Equal(200, output.Outputs["RocEma"].Values[3]);
        }
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(2, 3, 2)]
    [InlineData(20, 1, 3)]
    public async Task SevenIndependentOutputContractsAndLifecycle(
        int period,
        int ema,
        int deviation
    )
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(RateOfChangeRmsBands),
                "periods",
                () => new RateOfChangeRmsBands(period, ema, deviation)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public void EmaPeriodVariantsMatchFullSeries(int ema)
    {
        foreach (var period in new[] { 1, 2, 3, 20 })
        foreach (var shape in new[] { "walk", "zero", "alternating" })
            ComparisonVerifier.Check(
                SkenderRocBandsComparison.Create(ema),
                ComparisonVerifier.Fixture(shape, 83),
                period
            );
    }

    [Fact]
    public async Task SquaredReturnOverflowDoesNotDestroyFiniteBands()
    {
        var indicator = new RateOfChangeRmsBands(1, 1, 1);
        using var run = await Run(indicator, [1d, 1e306]);
        Assert.Equal(1e308, run[indicator.UpperBand][1]);
        Assert.Equal(-1e308, run[indicator.LowerBand][1]);
        Assert.Equal(1e308, run[indicator.Ema][1]);
        var huge = new RateOfChangeRmsBands(int.MaxValue, int.MaxValue, int.MaxValue);
        using var shortRun = await Run(huge, [1d, 2, 3]);
        Assert.Equal(new double[3], shortRun[huge.BandsAreDefined].ToArray());
        await Assert.ThrowsAsync<IndicatorOutputException>(async () =>
        {
            using var rejected = await Run(indicator, [double.Epsilon, 1]);
        });
    }

    [Fact]
    public void PeriodBoundsAreExplicit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RateOfChangeRmsBands(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RateOfChangeRmsBands(3, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RateOfChangeRmsBands(3, 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RateOfChangeRmsBands(3, 1, 4));
    }

    private static ComparisonOutput Output(int first, double?[] values) =>
        new(
            first,
            values.Select(v => v ?? double.NaN).ToArray(),
            values.Select(v => v.HasValue).ToArray()
        );

    private static Task<IIndicatorRun> Run(RateOfChangeRmsBands indicator, double[] values) =>
        new StockIndicatorBuilder()
            .ConfigureSource(
                Bars.From(
                    values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 1))
                )
            )
            .ConfigureIndicators(indicator)
            .BuildAsync();
}

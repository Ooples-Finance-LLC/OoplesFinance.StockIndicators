using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class ExtrapolatedAverageComparisonTests
{
    public static IEnumerable<object[]> Pairs =>
        ExtrapolatedAverageComparison.Pairs.Select(p => new object[] { p.Id });

    [Theory]
    [MemberData(nameof(Pairs))]
    public void DistinctSeedsMatchHandCalculatedFullTrajectories(string id)
    {
        var data = CompetitorData.FromCloses([3d, 6, 9, 12, 3, 6, 15, 9, 0]);
        var expected = id switch
        {
            "Skender.GetDema" => new ComparisonSeries(
                2,
                [double.NaN, double.NaN, 6, 10.5, 5.25, 5.625, 12.5625, 10.40625, 2.765625]
            ),
            "Skender.GetTema" => new ComparisonSeries(
                2,
                [double.NaN, double.NaN, 6, 11.25, 4.5, 5.4375, 13.6875, 10.265625, 1.3125]
            ),
            "TaLib.Functions.Dema" => new ComparisonSeries(
                4,
                [double.NaN, double.NaN, double.NaN, double.NaN, 5, 5.5, 12.5, 10.375, 2.75]
            ),
            _ => new ComparisonSeries(
                6,
                [
                    double.NaN,
                    double.NaN,
                    double.NaN,
                    double.NaN,
                    double.NaN,
                    double.NaN,
                    40d / 3,
                    485d / 48,
                    119d / 96,
                ]
            ),
        };
        var pair = ComparisonPairs.Get(id);
        ComparisonVerifier.Compare(expected, pair.Reference!(data, 3), "reference");
        ComparisonVerifier.Compare(expected, pair.Ooples(data, 3), "Ooples");
        ComparisonVerifier.Compare(expected, pair.Competitor(data, 3), "competitor");
    }

    [Theory]
    [InlineData(2, ExponentialSeedMode.Shared)]
    [InlineData(3, ExponentialSeedMode.Shared)]
    [InlineData(2, ExponentialSeedMode.Cascaded)]
    [InlineData(3, ExponentialSeedMode.Cascaded)]
    public async Task ExactContractsAndLifecycle(int order, ExponentialSeedMode mode)
    {
        foreach (var period in new[] { 1, 3, 20 })
        {
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    typeof(SeededExponentialAverage),
                    "seed",
                    () => new SeededExponentialAverage(period, order, mode)
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task ExtremeConstantsAndPeriodOnePreservePrices(int order)
    {
        foreach (var mode in new[] { ExponentialSeedMode.Shared, ExponentialSeedMode.Cascaded })
        {
            var indicator = new SeededExponentialAverage(3, order, mode);
            var result = await Run(indicator, Enumerable.Repeat(double.MaxValue, 12).ToArray());
            Assert.All(
                result.Skip(indicator.WarmupBars),
                value => Assert.Equal(double.MaxValue, value)
            );
            var values = new[]
            {
                double.MaxValue,
                -double.MaxValue,
                double.Epsilon,
                -double.Epsilon,
            };
            Assert.Equal(values, await Run(new SeededExponentialAverage(1, order, mode), values));
            var huge = new SeededExponentialAverage(int.MaxValue, order, mode);
            Assert.Equal(new double[3], await Run(huge, [1d, 2, 3]));
        }
        ComparisonVerifier.Check(
            ComparisonPairs.Get(order == 2 ? "Skender.GetDema" : "Skender.GetTema"),
            CompetitorData.FromCloses([3d, 1, -2, 0]),
            1
        );
    }

    [Theory]
    [InlineData("TaLib.Functions.Dema")]
    [InlineData("TaLib.Functions.Tema")]
    public void PinnedTaLibRejectsPeriodOne(string id)
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            ComparisonPairs.Get(id).Competitor(CompetitorData.FromCloses([1d, 2, 3]), 1)
        );
        Assert.Contains("BadParam", error.Message);
    }

    [Fact]
    public void InvalidParametersAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SeededExponentialAverage(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SeededExponentialAverage(2, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SeededExponentialAverage(2, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SeededExponentialAverage(2, 2, (ExponentialSeedMode)99)
        );
    }

    private static async Task<double[]> Run(SeededExponentialAverage indicator, double[] values)
    {
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(
                Bars.From(
                    values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 1))
                )
            )
            .ConfigureIndicators(indicator)
            .BuildAsync();
        return run[indicator.Outputs[0]].ToArray();
    }
}

using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class ObvComparisonTests
{
    [Fact]
    public void GoldenSeedsAndOptionalAverages()
    {
        var data = ObvComparison.Fixture();
        foreach (var pair in ObvComparison.Pairs)
        {
            var name = pair.Id.StartsWith("Skender", StringComparison.Ordinal) ? "Obv" : "Value";
            double[] expected =
                name == "Obv" ? [0, 3, 3, -2, 5, 5, 5] : [10, 13, 13, 8, 15, 15, 15];
            Assert.Equal(expected, pair.Competitor(data, 2).Outputs[name].Values);
            Assert.Equal(expected, pair.Ooples(data, 2).Outputs[name].Values);
            ComparisonVerifier.Check(pair, data, 2);
        }
        foreach (int? period in new int?[] { null, 1, 2, 5 })
            ComparisonVerifier.Check(ObvComparison.Skender(period), data, 2);
        var average = ObvComparison.Skender(2).Competitor(data, 2).Outputs["ObvSma"];
        Assert.Equal(new[] { false, false, true, true, true, true, true }, average.Present);
        Assert.Equal(new double[] { 3, .5, 1.5, 5, 5 }, average.Values.Skip(2));
    }

    [Fact]
    public void TradyTupleAndGenericEntryPoints()
    {
        (decimal Close, decimal Volume)[] input =
        [
            (-2, 10),
            (-1, 3),
            (-1, 100),
            (-3, 5),
            (0, 7),
            (0, 1000),
            (2, 0),
        ];
        decimal?[] expected = [10, 13, 13, 8, 15, 15, 15];
        Assert.Equal(
            expected,
            new Trady.Analysis.Indicator.OnBalanceVolumeByTuple(input).Compute()
        );
        Assert.Equal(
            expected,
            new Trady.Analysis.Indicator.OnBalanceVolume<(decimal Close, decimal Volume), decimal?>(
                input,
                x => x
            ).Compute()
        );
    }

    [Fact]
    public void TaLibSingletonBoundary()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            ComparisonPairs
                .Get("TaLib.Functions.Obv")
                .Competitor(CompetitorData.FromCloses([0d]), 2)
        );
        Assert.Contains("OutOfRange", error.Message);
    }

    [Theory]
    [InlineData(ObvSeed.Zero)]
    [InlineData(ObvSeed.FirstVolume)]
    public async Task ExactReferenceAndLifecycle(ObvSeed seed)
    {
        foreach (int? period in new int?[] { null, 1, 3 })
        {
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    typeof(SeededOnBalanceVolume),
                    "seeded",
                    () => new SeededOnBalanceVolume(seed, period)
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
    }

    [Fact]
    public async Task StrictPriceChangesCancellationAndOverflow()
    {
        var outputs = await Run(
            ObvSeed.FirstVolume,
            2,
            [0, double.Epsilon, 0],
            [double.MaxValue, 0, double.MaxValue]
        );
        Assert.Equal(new[] { double.MaxValue, double.MaxValue, 0d }, outputs[0]);
        Assert.Equal(double.MaxValue / 2, outputs[1][2]);
        Assert.Equal(
            new double[] { 0, 3, 1 },
            (await Run(ObvSeed.Zero, null, [0, double.Epsilon, 0], [10, 3, 2]))[0]
        );
        Assert.Equal(
            new double[3],
            (await Run(ObvSeed.Zero, int.MaxValue, [-1, 0, 1], [1, 2, 3]))[2]
        );
        Assert.Equal(
            new double[] { double.MaxValue, double.MaxValue, 1 },
            (
                await Run(
                    ObvSeed.FirstVolume,
                    null,
                    [0, 1, 0],
                    [double.MaxValue, 1, double.MaxValue]
                )
            )[0]
        );
        await Assert.ThrowsAsync<IndicatorOutputException>(() =>
            Run(ObvSeed.FirstVolume, null, [0, 1], [double.MaxValue, double.MaxValue])
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => new SeededOnBalanceVolume((ObvSeed)99));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SeededOnBalanceVolume(averagePeriod: 0)
        );
    }

    private static async Task<double[][]> Run(
        ObvSeed seed,
        int? period,
        double[] prices,
        double[] volumes
    )
    {
        var indicator = new SeededOnBalanceVolume(seed, period);
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(
                Bars.From(
                    prices.Select(
                        (p, i) => new Bar(DateTime.UnixEpoch.AddDays(i), p, p, p, p, volumes[i])
                    )
                )
            )
            .ConfigureIndicators(indicator)
            .BuildAsync();
        return indicator.Outputs.Select(output => run[output].ToArray()).ToArray();
    }
}

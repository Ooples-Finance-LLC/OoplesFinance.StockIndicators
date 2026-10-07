using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class PriceChangeComparisonTests
{
    public static IEnumerable<object[]> Forms =>
        PriceChangeComparison.Forms.Select(f => new object[] { f.Name, f.Kind });

    [Theory]
    [MemberData(nameof(Forms))]
    public void HandCalculatedNegativeAndZeroDenominators(string name, PriceChangeKind kind)
    {
        var data = CompetitorData.FromCloses([4d, -2, 0, 8, -4, 5, 2]);
        double[] expected = kind switch
        {
            PriceChangeKind.Difference => [double.NaN, double.NaN, -4, 10, -4, -3, 6],
            PriceChangeKind.Fraction => [double.NaN, double.NaN, -1, -5, 0, -0.375, -1.5],
            PriceChangeKind.Percent => [double.NaN, double.NaN, -100, -500, 0, -37.5, -150],
            PriceChangeKind.Ratio => [double.NaN, double.NaN, 0, -4, 0, 0.625, -0.5],
            _ => [double.NaN, double.NaN, 0, -400, 0, 62.5, -50],
        };
        var pair = ComparisonPairs.Get("TaLib.Functions." + name);
        var golden = new ComparisonSeries(2, expected);
        ComparisonVerifier.Compare(golden, pair.Reference!(data, 2), "reference");
        ComparisonVerifier.Compare(golden, pair.Ooples(data, 2), "Ooples");
        ComparisonVerifier.Compare(golden, pair.Competitor(data, 2), "TA-Lib");
    }

    [Theory]
    [MemberData(nameof(Forms))]
    public void PeriodOneRetainsEveryTransition(string name, PriceChangeKind _)
    {
        var pair = ComparisonPairs.Get("TaLib.Functions." + name);
        Assert.Equal(
            4,
            ComparisonVerifier.Check(pair, CompetitorData.FromCloses([2d, 4, 0, -2, 1]), 1)
        );
    }

    [Theory]
    [MemberData(nameof(Forms))]
    public async Task ExactContractAndLifecycle(string _, PriceChangeKind kind)
    {
        foreach (var period in new[] { 1, 3, 20 })
        {
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    typeof(LaggedPriceChange),
                    "formula",
                    () => new LaggedPriceChange(period, kind)
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
    }

    [Fact]
    public async Task FiniteResultsSurviveOverflowingIntermediateDifference()
    {
        var inputs = new[] { double.MaxValue, -double.MaxValue };
        Assert.Equal(-2, (await Run(PriceChangeKind.Fraction, inputs))[^1]);
        Assert.Equal(-200, (await Run(PriceChangeKind.Percent, inputs))[^1]);
        Assert.Equal(-1, (await Run(PriceChangeKind.Ratio, inputs))[^1]);
        Assert.Equal(-100, (await Run(PriceChangeKind.RatioPercent, inputs))[^1]);
        await Assert.ThrowsAsync<IndicatorOutputException>(() =>
            Run(PriceChangeKind.Difference, inputs)
        );
        Assert.Equal(
            0,
            (await Run(PriceChangeKind.Difference, [double.MaxValue, double.MaxValue]))[^1]
        );
        Assert.Equal(
            1,
            (await Run(PriceChangeKind.Fraction, [double.Epsilon, 2 * double.Epsilon]))[^1]
        );
        Assert.Equal(
            100,
            (await Run(PriceChangeKind.Percent, [double.Epsilon, 2 * double.Epsilon]))[^1]
        );
        Assert.Equal(
            double.Epsilon,
            (await Run(PriceChangeKind.Difference, [double.Epsilon, 2 * double.Epsilon]))[^1]
        );
    }

    [Fact]
    public void InvalidParametersAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LaggedPriceChange(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LaggedPriceChange(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LaggedPriceChange(1, (PriceChangeKind)999)
        );
        Assert.Equal(int.MaxValue, new LaggedPriceChange(int.MaxValue).WarmupBars);
    }

    [Fact]
    public async Task MaximumPeriodKeepsStartupStorageBounded()
    {
        Assert.Equal(new double[3], await Run(PriceChangeKind.Percent, [1d, 2, 3], int.MaxValue));
    }

    private static async Task<double[]> Run(PriceChangeKind kind, double[] values, int period = 1)
    {
        var indicator = new LaggedPriceChange(period, kind);
        var bars = values
            .Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 1))
            .ToArray();
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        return run[indicator.Outputs[0]].ToArray();
    }
}

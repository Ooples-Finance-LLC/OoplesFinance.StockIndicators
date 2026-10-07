using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;
using T = Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class TradyReturnComparisonTests
{
    [Theory]
    [InlineData("RateOfChange")]
    [InlineData("PercentageDifference")]
    public void MissingValuesDisappearAndResumeWithoutLosingRealZeroReturns(string name)
    {
        var data = CompetitorData.FromOhlc(
            [1d, 2, 3, 4, 5, 6, 7],
            [10d, 11, 12, 13, 14, 15, 16],
            [-10d, -11, -12, -13, -14, -15, -16],
            [0d, 2, 4, 0, -2, 0, -2]
        );
        var expected = TradyReturnComparison.Series(
            2,
            [double.NaN, double.NaN, double.NaN, -100, -150, double.NaN, 0],
            [false, false, false, true, true, false, true]
        );
        var pair = ComparisonPairs.Get("Trady.Indicator." + name);
        ComparisonVerifier.Compare(expected, pair.Reference!(data, 2), "reference");
        ComparisonVerifier.Compare(expected, pair.Ooples(data, 2), "Ooples");
        ComparisonVerifier.Compare(expected, pair.Competitor(data, 2), "Trady");
        Assert.Equal(3, ComparisonVerifier.Check(pair, data, 1));
    }

    [Fact]
    public void TupleEntryPointsPreserveMissingMatureValues()
    {
        decimal?[] input = [0, 2, 4, 0, -2, 0, -2];
        decimal?[] expected = [null, null, null, -100, -150, null, 0];
        Assert.Equal(expected, new T.PercentageDifferenceByTuple(input, 2).Compute());
        Assert.Equal(
            expected,
            new T.PercentageDifferenceByTuple(input.Select(x => x!.Value), 2).Compute()
        );
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(20)]
    public async Task BothOutputContractsAndLifecycle(int period)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(RateOfChangeWithValidity),
                "period",
                () => new RateOfChangeWithValidity(period)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public async Task PublicValidityDistinguishesMissingAndUnchangedPrices()
    {
        var result = await Run([0d, 2, 2, 0, 4]);
        Assert.Equal(new[] { 0d, 0, 0, -100, 0 }, result.Value);
        Assert.Equal(new[] { 0d, 0, 1, 1, 0 }, result.Valid);
        result = await Run([double.MaxValue, -double.MaxValue]);
        Assert.Equal(-200, result.Value[1]);
        Assert.Equal(1, result.Valid[1]);
        result = await Run([double.Epsilon, 2 * double.Epsilon]);
        Assert.Equal(100, result.Value[1]);
        await Assert.ThrowsAsync<IndicatorOutputException>(() =>
            Run([double.Epsilon, double.MaxValue])
        );
        Assert.Equal(new double[3], (await Run([1d, 2, 3], int.MaxValue)).Valid);
    }

    [Fact]
    public void PeriodMustBePositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RateOfChangeWithValidity(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RateOfChangeWithValidity(-1));
    }

    private static async Task<(double[] Value, double[] Valid)> Run(double[] values, int period = 1)
    {
        var indicator = new RateOfChangeWithValidity(period);
        var bars = values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 1));
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        return (run[indicator.Value].ToArray(), run[indicator.IsDefined].ToArray());
    }
}

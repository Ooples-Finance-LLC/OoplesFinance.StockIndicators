using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;
using T = Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class TradyMomentumComparisonTests
{
    public static IEnumerable<object[]> Forms =>
        TradyMomentumComparison.Forms.Select(f => new object[] { f.Name, f.Kind });

    [Theory]
    [MemberData(nameof(Forms))]
    public void SignedDifferencesZerosAndPlateaus(string name, PriceChangeKind kind)
    {
        var data = CompetitorData.FromOhlc(
            [1d, 2, 3, 4, 5, 6, 7],
            [10d, 11, 12, 13, 14, 15, 16],
            [-10d, -11, -12, -13, -14, -15, -16],
            [2d, -4, 0, 6, -3, 6, -3]
        );
        double[] expected = kind switch
        {
            PriceChangeKind.Gain => [double.NaN, double.NaN, 0, 10, 0, 0, 0],
            PriceChangeKind.Loss => [double.NaN, double.NaN, 2, 0, 3, 0, 0],
            _ => [double.NaN, double.NaN, -2, 10, -3, 0, 0],
        };
        var pair = ComparisonPairs.Get("Trady.Indicator." + name);
        var golden = new ComparisonSeries(2, expected);
        ComparisonVerifier.Compare(golden, pair.Reference!(data, 2), "reference");
        ComparisonVerifier.Compare(golden, pair.Ooples(data, 2), "Ooples");
        ComparisonVerifier.Compare(golden, pair.Competitor(data, 2), "Trady");
        Assert.Equal(6, ComparisonVerifier.Check(pair, data, 1));
    }

    [Theory]
    [InlineData(PriceChangeKind.Gain)]
    [InlineData(PriceChangeKind.Loss)]
    public async Task ExactReferenceAndLifecycle(PriceChangeKind kind)
    {
        foreach (var period in new[] { 1, 3, 20 })
        {
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    typeof(LaggedPriceChange),
                    "directional",
                    () => new LaggedPriceChange(period, kind)
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
    }

    [Fact]
    public void TupleEntryPointsMatchTheGenericDefinitions()
    {
        decimal?[] input = [2, -4, 0, 6, -3, 6, -3];
        Assert.Equal(
            new decimal?[] { null, null, -2, 10, -3, 0, 0 },
            new T.DifferenceByTuple(input, 2).Compute()
        );
        Assert.Equal(
            new decimal?[] { null, null, 0, 10, 0, 0, 0 },
            new T.PositiveDifferenceByTuple(input, 2).Compute()
        );
        Assert.Equal(
            new decimal?[] { null, null, 2, 0, 3, 0, 0 },
            new T.NegativeDifferenceByTuple(input, 2).Compute()
        );
    }

    [Fact]
    public async Task OppositeDirectionIsZeroEvenWhenTheRawDifferenceOverflows()
    {
        Assert.Equal(0, await Last(PriceChangeKind.Gain, double.MaxValue, -double.MaxValue));
        Assert.Equal(0, await Last(PriceChangeKind.Loss, -double.MaxValue, double.MaxValue));
        Assert.Equal(double.Epsilon, await Last(PriceChangeKind.Gain, 0, double.Epsilon));
        Assert.Equal(double.Epsilon, await Last(PriceChangeKind.Loss, double.Epsilon, 0));
        await Assert.ThrowsAsync<IndicatorOutputException>(() =>
            Last(PriceChangeKind.Gain, -double.MaxValue, double.MaxValue)
        );
        await Assert.ThrowsAsync<IndicatorOutputException>(() =>
            Last(PriceChangeKind.Loss, double.MaxValue, -double.MaxValue)
        );
    }

    private static async Task<double> Last(PriceChangeKind kind, double previous, double current)
    {
        var indicator = new LaggedPriceChange(1, kind);
        var bars = new[] { previous, current }.Select(
            (v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 1)
        );
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        return run[indicator.Outputs[0]][1];
    }
}

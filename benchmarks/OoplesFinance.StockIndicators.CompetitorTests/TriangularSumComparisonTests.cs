using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class TriangularSumComparisonTests
{
    public static IEnumerable<object[]> Pairs =>
        TriangularSumComparison.Pairs.Select(p => new object[] { p.Id });

    [Theory]
    [MemberData(nameof(Pairs))]
    public void OddAndEvenWindowsMatchHandCalculatedValues(string id)
    {
        var data = CompetitorData.FromOhlc(
            [1d, 2, 3, 4, 5, 6],
            [20d, 21, 22, 23, 24, 25],
            [-20d, -21, -22, -23, -24, -25],
            [3d, 0, 6, 12, -3, 9]
        );
        var sum = id.EndsWith("Sum", StringComparison.Ordinal);
        var odd = new ComparisonSeries(
            2,
            sum
                ? [double.NaN, double.NaN, 9, 18, 15, 18]
                : [double.NaN, double.NaN, 2.25, 6, 6.75, 3.75]
        );
        var even = new ComparisonSeries(
            3,
            sum
                ? [double.NaN, double.NaN, double.NaN, 21, 15, 24]
                : [double.NaN, double.NaN, double.NaN, 4.5, 5.5, 5.5]
        );
        var pair = ComparisonPairs.Get(id);
        foreach (var (period, expected) in new[] { (3, odd), (4, even) })
        {
            ComparisonVerifier.Compare(expected, pair.Reference!(data, period), "reference");
            ComparisonVerifier.Compare(expected, pair.Ooples(data, period), "Ooples");
            ComparisonVerifier.Compare(expected, pair.Competitor(data, period), "competitor");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExactReferencesAndLifecycle(bool sum)
    {
        foreach (var period in new[] { 1, 2, 3, 4, 20 })
        {
            IIndicator Create() =>
                sum ? new RollingPriceSum(period) : new TriangularWindowAverage(period);
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(Create().GetType(), "window", Create)
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
    }

    [Fact]
    public async Task ExactTriangularConvolutionPreservesExtremeAndSubnormalInputs()
    {
        foreach (var period in new[] { 3, 4 })
        {
            var output = await Run(
                new TriangularWindowAverage(period),
                Enumerable.Repeat(double.MaxValue, 12).ToArray()
            );
            Assert.All(output.Skip(period - 1), value => Assert.Equal(double.MaxValue, value));
        }
        Assert.Equal(
            0,
            (await Run(new TriangularWindowAverage(3), [-double.MaxValue, 0, double.MaxValue]))[2]
        );
        Assert.Equal(
            0,
            (
                await Run(
                    new TriangularWindowAverage(3),
                    [-double.Epsilon, double.Epsilon, double.Epsilon]
                )
            )[2]
        );
        Assert.Equal(
            double.Epsilon,
            (
                await Run(
                    new TriangularWindowAverage(3),
                    [double.Epsilon, double.Epsilon, double.Epsilon]
                )
            )[2]
        );
        Assert.Equal(
            new double[3],
            await Run(new TriangularWindowAverage(int.MaxValue), [1d, 2, 3])
        );
    }

    [Fact]
    public async Task RollingSumDistinguishesCancellationFromTrueOverflow()
    {
        Assert.Equal(
            new double[3],
            await Run(new RollingPriceSum(2), [double.MaxValue, -double.MaxValue, double.MaxValue])
        );
        Assert.Equal(
            2 * double.Epsilon,
            (await Run(new RollingPriceSum(2), [double.Epsilon, double.Epsilon]))[1]
        );
        await Assert.ThrowsAsync<IndicatorOutputException>(() =>
            Run(new RollingPriceSum(2), [double.MaxValue, double.MaxValue])
        );
        Assert.Equal(new double[3], await Run(new RollingPriceSum(int.MaxValue), [1d, 2, 3]));
    }

    [Fact]
    public async Task PeriodOneAndInvalidPeriodsAreExplicit()
    {
        var values = new[] { double.MaxValue, -double.MaxValue, double.Epsilon };
        Assert.Equal(values, await Run(new TriangularWindowAverage(1), values));
        Assert.Equal(values, await Run(new RollingPriceSum(1), values));
        ComparisonVerifier.Check(
            ComparisonPairs.Get("QuanTAlib.Trima"),
            CompetitorData.FromCloses([3d, 0, -2]),
            1
        );
        foreach (var id in new[] { "TaLib.Functions.Trima", "TaLib.Functions.Sum" })
        {
            var error = Assert.Throws<InvalidOperationException>(() =>
                ComparisonPairs.Get(id).Competitor(CompetitorData.FromCloses([1d, 2, 3]), 1)
            );
            Assert.Contains("BadParam", error.Message);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => new TriangularWindowAverage(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RollingPriceSum(0));
    }

    private static async Task<double[]> Run(IIndicator indicator, double[] values)
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

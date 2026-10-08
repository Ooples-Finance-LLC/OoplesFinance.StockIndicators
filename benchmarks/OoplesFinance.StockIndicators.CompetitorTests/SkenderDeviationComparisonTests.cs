using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SkenderDeviationComparisonTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    [InlineData(3)]
    public async Task IndependentRationalOutputsAndFlagsCoverLifecycleAndExtremeInputs(int? smooth)
    {
        foreach (var period in new[] { 3, 20 })
        {
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    typeof(StandardDeviationWithDetails),
                    "complete deviation outputs",
                    () => new StandardDeviationWithDetails(period, smooth)
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
    }

    private static async Task<double[][]> Run(int period, int? smooth, params double[] values)
    {
        var indicator = new StandardDeviationWithDetails(period, smooth);
        var bars = values
            .Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 0))
            .ToArray();
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        return indicator.Outputs.Select(o => run[o].ToArray()).ToArray();
    }

    [Fact]
    public async Task SmoothingStartsAfterItsOwnFullWindowAndCanBeDisabled()
    {
        var values = await Run(2, 2, 1, 3, 3, 3, 5);
        Assert.Equal(new[] { 0d, 1, 0, 0, 1 }, values[0]);
        Assert.Equal(new[] { 0d, 2, 3, 3, 4 }, values[1]);
        Assert.Equal(new[] { 0d, 1, 0, 0, 1 }, values[2]);
        Assert.Equal(new[] { 0d, 0, .5, 0, .5 }, values[3]);
        Assert.Equal(new[] { 0d, 1, 1, 1, 1 }, values[4]);
        Assert.Equal(values[4], values[5]);
        Assert.Equal(new[] { 0d, 1, 0, 0, 1 }, values[6]);
        Assert.Equal(new[] { 0d, 0, 1, 1, 1 }, values[7]);
        var disabled = await Run(2, null, 1, 3, 3, 3, 5);
        Assert.All(disabled[3], v => Assert.Equal(0, v));
        Assert.All(disabled[7], v => Assert.Equal(0, v));
        var identity = await Run(2, 1, 1, 3, 3, 3, 5);
        Assert.Equal(identity[0], identity[3]);
        Assert.Equal(identity[4], identity[7]);
    }

    [Fact]
    public async Task ExactFlatnessAndSubnormalNormalizationHaveExplicitPresence()
    {
        var flat = await Run(3, 1, .1, .1, .1);
        Assert.Equal(0, flat[0][2]);
        Assert.Equal(0, flat[6][2]);
        var native = CompetitorData.FromCloses([.1, .1, .1]).Quotes.GetStdDev(3, 1).Last();
        Assert.True(native.StdDev > 0);
        Assert.Equal(-1, native.ZScore);
        var tiny = await Run(2, 2, 0, double.Epsilon, 2 * double.Epsilon);
        Assert.Equal(new[] { 0d, 0, 0 }, tiny[0]);
        Assert.Equal(new[] { 0d, 1, 1 }, tiny[2]);
        Assert.Equal(new[] { 0d, 1, 1 }, tiny[6]);
        Assert.Equal(new[] { 0d, 0, 1 }, tiny[7]);
        var tuple = new[]
        {
            (DateTime.UnixEpoch, 0d),
            (DateTime.UnixEpoch.AddDays(1), double.Epsilon),
        }
            .GetStdDev(2)
            .Last();
        Assert.Equal(0, tuple.StdDev);
        Assert.Null(tuple.ZScore);
    }

    [Fact]
    public async Task FiniteExtremeStatisticsAndMaximumPeriodsDoNotOverflowOrAllocateEagerly()
    {
        var huge = await Run(2, 1, -double.MaxValue, double.MaxValue);
        Assert.Equal(double.MaxValue, huge[0][1]);
        Assert.Equal(0, huge[1][1]);
        Assert.Equal(1, huge[2][1]);
        Assert.Equal(double.MaxValue, huge[3][1]);
        var longPrice = await Run(int.MaxValue, 3, 1, 2, 3);
        Assert.All(longPrice, row => Assert.All(row, v => Assert.Equal(0, v)));
        var longSmooth = await Run(3, int.MaxValue, 1, 2, 3);
        Assert.Equal(1, longSmooth[4][2]);
        Assert.Equal(0, longSmooth[7][2]);
        Assert.Throws<OverflowException>(() =>
            CompetitorData.FromCloses([1, 2, 3]).Quotes.GetStdDev(int.MaxValue, 3).ToArray()
        );
    }

    [Fact]
    public void CompleteComparisonsCoverStartupFlatnessAndQuoteConversion()
    {
        foreach (var smooth in new int?[] { null, 1, 3, 20 })
        foreach (var period in new[] { 2, 3, 20 })
        {
            var pair = SkenderDeviationComparison.Create(smooth);
            ComparisonVerifier.Check(pair, DispersionComparison.Fixture(), period);
            ComparisonVerifier.Check(
                pair,
                SkenderDeviationComparison.FlatRoundingFixture(),
                period
            );
            ComparisonVerifier.Check(
                pair,
                SkenderDeviationComparison.CollapsedQuoteFixture(),
                period
            );
            ComparisonVerifier.Check(pair, CompetitorData.FromCloses([5]), period);
        }
        var collapse = SkenderDeviationComparison.CollapsedQuoteFixture();
        var configured = SkenderDeviationComparison.Create(1);
        Assert.True(configured.Ooples(collapse, 2).Outputs["ZScore"].Present![1]);
        Assert.False(configured.Competitor(collapse, 2).Outputs["ZScore"].Present![1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => new StandardDeviationWithDetails(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StandardDeviationWithDetails(2, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => collapse.Quotes.GetStdDev(1).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => collapse.Quotes.GetStdDev(2, 0).ToArray());
    }

    [Fact]
    public void QuoteTupleAndChainedNativeRoutesAgreeOnExactlyRepresentablePrices()
    {
        var data = DispersionComparison.Fixture();
        var quotes = data.Quotes.GetStdDev(3, 2);
        var tuples = data.Dates.Zip(data.Closes, (date, value) => (date, value)).GetStdDev(3, 2);
        var chained = data.Quotes.GetSma(1).Cast<IReusableResult>().GetStdDev(3, 2);
        static object[] Fields(IEnumerable<StdDevResult> rows) =>
            rows.Select(r => (object)(r.StdDev, r.Mean, r.ZScore, r.StdDevSma)).ToArray();
        Assert.Equal(Fields(quotes), Fields(tuples));
        Assert.Equal(Fields(quotes), Fields(chained));
    }

    [Fact]
    public void AllValuesAndPresenceFlagsRejectCorruptionForEverySmoothingMode()
    {
        foreach (var smooth in new int?[] { null, 1, 3 })
        foreach (var native in new[] { false, true })
        foreach (var name in SkenderDeviationComparison.Names)
        foreach (var presence in new[] { false, true })
        {
            var pair = SkenderDeviationComparison.Create(smooth);
            ComparisonSeries Bad(CompetitorData data, int period)
            {
                var result = native ? pair.Competitor(data, period) : pair.Ooples(data, period);
                var output = result.Outputs[name];
                var index = Array.FindLastIndex(output.Present!, v => v);
                if (index < 0)
                    index = data.Count - 1;
                if (presence)
                    output.Present![index] = !output.Present[index];
                else
                    output.Values[index] = output.Present![index] ? output.Values[index] + 1 : 42;
                return result;
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    DispersionComparison.Fixture(),
                    3
                )
            );
        }
    }

    [Fact]
    public void IndependentSquareRootOracleCertifiesTiesSubnormalsAndOverflow()
    {
        var grid = BigInteger.One << 1074;
        Assert.Equal(0, DispersionReferenceArithmetic.Sqrt(1, 4 * grid * grid));
        Assert.Equal(2 * double.Epsilon, DispersionReferenceArithmetic.Sqrt(9, 4 * grid * grid));
        Assert.Equal(Math.Sqrt(2), DispersionReferenceArithmetic.Sqrt(2, 1));
        Assert.Equal(1.5, DispersionReferenceArithmetic.Sqrt(9, 4));
        var maximum = MoneyFlowReferenceArithmetic.Units(double.MaxValue);
        Assert.Equal(
            double.MaxValue,
            DispersionReferenceArithmetic.Sqrt(maximum * maximum, grid * grid)
        );
        Assert.Equal(
            double.PositiveInfinity,
            DispersionReferenceArithmetic.Sqrt(BigInteger.One << 2048, 1)
        );
    }
}

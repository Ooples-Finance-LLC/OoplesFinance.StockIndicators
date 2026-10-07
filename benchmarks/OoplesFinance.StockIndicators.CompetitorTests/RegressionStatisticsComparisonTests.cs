using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class RegressionStatisticsComparisonTests
{
    [Fact]
    public async Task IndependentRationalStatisticsCoverLifecycleAndOverflow()
    {
        foreach (var period in new[] { 2, 3, 20 })
        {
            var report = await IndicatorValidation.ValidateAsync(
                new IndicatorValidationCase(
                    typeof(WindowRegressionStatistics),
                    "rolling regression statistics",
                    () => new WindowRegressionStatistics(period)
                )
            );
            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        }
    }

    [Fact]
    public async Task CoordinatesAndFlatWindowPresenceAreExplicit()
    {
        var indicator = new WindowRegressionStatistics(3);
        var data = CompetitorData.FromCloses([1, 2, 3, 3, 3]);
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        Assert.Equal(new[] { 0d, 1, 1, .5, 0 }, run[indicator.Slope].ToArray());
        Assert.Equal(new[] { 0d, 0, 0, 5d / 3, 3 }, run[indicator.Intercept].ToArray());
        Assert.Equal(
            new[] { 0d, 0.5, Math.Sqrt(2d / 3), Math.Sqrt(2d / 9), 0 },
            run[indicator.StandardDeviation].ToArray()
        );
        Assert.Equal(new[] { 0d, 1, 1, .75, 0 }, run[indicator.RSquared].ToArray());
        Assert.Equal(new[] { 0d, 1, 1, 1, 0 }, run[indicator.RSquaredIsDefined].ToArray());
        Assert.Equal(new[] { 0d, 1, 1, 1, 1 }, run[indicator.LineIsDefined].ToArray());
        var native = RegressionStatisticsComparison.Pair.Competitor(data, 3).Outputs["RSquared"];
        Assert.True(native.Present![4]);
        Assert.Equal(.75, native.Values[4], 12);
    }

    [Fact]
    public async Task SubnormalVarianceDoesNotEraseDefinedCorrelationAndMaximumPeriodIsLazy()
    {
        var indicator = new WindowRegressionStatistics(int.MaxValue);
        var bars = new[] { 0d, 2 * double.Epsilon, 4 * double.Epsilon }
            .Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 0))
            .ToArray();
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        Assert.Equal(
            new[] { 0d, double.Epsilon, 2 * double.Epsilon },
            run[indicator.StandardDeviation].ToArray()
        );
        Assert.Equal(new[] { 0d, 1, 1 }, run[indicator.RSquared].ToArray());
        Assert.Equal(
            new[] { 0d, 2 * double.Epsilon, 2 * double.Epsilon },
            run[indicator.Slope].ToArray()
        );
    }

    [Fact]
    public void AllNativeFieldsAndTheirPresenceHaveIndependentReferences()
    {
        foreach (
            var pair in new[]
            {
                RegressionStatisticsComparison.Pair,
                RegressionStatisticsComparison.SnapshotPair,
            }
        )
        foreach (var period in new[] { 2, 3, 20 })
        {
            ComparisonVerifier.Check(pair, RegressionStatisticsComparison.Fixture(), period);
            ComparisonVerifier.Check(pair, CompetitorData.FromCloses([5]), period);
            ComparisonVerifier.Check(pair, CompetitorData.FromCloses([5, 5, 5, 5]), period);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowRegressionStatistics(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new QuanTAlib.Slope(1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CompetitorData.FromCloses([5, 5]).Quotes.GetSlope(1).ToArray()
        );
    }

    [Fact]
    public void EveryOutputAndPresenceFlagRejectsCorruptionInBothArms()
    {
        foreach (
            var pair in new[]
            {
                RegressionStatisticsComparison.Pair,
                RegressionStatisticsComparison.SnapshotPair,
            }
        )
        foreach (var native in new[] { false, true })
        foreach (var name in RegressionStatisticsComparison.Names)
        foreach (var presence in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData d, int p)
            {
                var result = native ? pair.Competitor(d, p) : pair.Ooples(d, p);
                var output = result.Outputs[name];
                var index = Array.FindLastIndex(output.Present!, v => v);
                Assert.True(index >= 0);
                if (presence)
                    output.Present![index] = !output.Present[index];
                else
                    output.Values[index] += 1;
                return result;
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    RegressionStatisticsComparison.Fixture(),
                    3
                )
            );
        }
    }

    [Fact]
    public void SnapshotOverlayIsRetrospectiveAndUsesGlobalCoordinates()
    {
        var bars = CompetitorData.FromCloses([1, 2, 4, 9]).IndicatorBars;
        var first = RegressionSnapshot.Calculate(bars.Take(3).ToArray(), 3);
        Assert.Equal(new double?[] { 5d / 6, 7d / 3, 23d / 6 }, first.Select(r => r.Line));
        Assert.Null(first[0].Slope);
        Assert.Null(first[1].Slope);
        Assert.Equal(-2d / 3, first[2].Intercept);
        var second = RegressionSnapshot.Calculate(bars, 3);
        Assert.Equal(new double?[] { null, 1.5, 5, 8.5 }, second.Select(r => r.Line));
        Assert.Equal(-5.5, second[3].Intercept);
        Assert.Equal(5d / 6, first[0].Line);
        Assert.All(
            RegressionSnapshot.Calculate(bars, int.MaxValue),
            row => Assert.Equal(new(null, null, null, null, null), row)
        );
        Assert.Empty(RegressionSnapshot.Calculate(Array.Empty<Bar>()));
    }

    [Fact]
    public void SnapshotRejectsInvalidInputsAndPreservesFiniteExtremeStatistics()
    {
        Bar[] BarsFor(params double[] values) =>
            values
                .Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 0))
                .ToArray();
        Assert.Throws<ArgumentNullException>(() => RegressionSnapshot.Calculate(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RegressionSnapshot.Calculate(BarsFor(1), 1)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RegressionSnapshot.Calculate(BarsFor(double.NaN))
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RegressionSnapshot.Calculate(BarsFor(double.PositiveInfinity))
        );
        Assert.Throws<OverflowException>(() =>
            RegressionSnapshot.Calculate(BarsFor(-double.MaxValue, double.MaxValue), 2)
        );
        var constant = RegressionSnapshot.Calculate(BarsFor(double.MaxValue, double.MaxValue), 2);
        Assert.Equal(double.MaxValue, constant[1].Intercept);
        Assert.Equal(0, constant[1].StandardDeviation);
        Assert.All(constant, row => Assert.Equal(double.MaxValue, row.Line));
        var subnormal = RegressionSnapshot.Calculate(
            BarsFor(0, 2 * double.Epsilon, 4 * double.Epsilon),
            3
        );
        Assert.Equal(1, subnormal[2].RSquared);
        Assert.Equal(2 * double.Epsilon, subnormal[2].StandardDeviation);
        Assert.Equal(
            new double?[] { 0, 2 * double.Epsilon, 4 * double.Epsilon },
            subnormal.Select(r => r.Line)
        );
    }

    [Fact]
    public async Task RegressionAngleRemainsFiniteWhenUnpublishedSlopeOverflows()
    {
        var indicator = new WindowLinearRegression(2, WindowRegressionOutput.Angle);
        var bars = new[] { -double.MaxValue, double.MaxValue, -double.MaxValue }
            .Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 0))
            .ToArray();
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator)
            .BuildAsync();
        Assert.Equal(new[] { 0d, 90, -90 }, run[indicator.Outputs[0]].ToArray());
    }
}

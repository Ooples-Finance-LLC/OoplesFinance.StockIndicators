using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class CurvatureComparisonTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(20)]
    public void CompositionIncludesEveryStartupAndStatistic(int period)
    {
        ComparisonVerifier.Check(CurvatureComparison.Pair, CurvatureComparison.Fixture(), period);
        var square = Enumerable.Range(0, 15).Select(i => (double)(i * i)).ToArray();
        var output = CurvatureComparison.Owned(square, 3).Outputs;
        Assert.Equal(new[] { 0d, 1, 1, 1.5, 2 }, output["Curvature"].Values.Take(5));
        Assert.All(output["Curvature"].Values.Skip(4), value => Assert.Equal(2, value));
        Assert.False(output["Intercept"].Present![0]);
        Assert.True(output["Curvature"].Present![0]);
    }

    [Fact]
    public void ExactIntermediateSlopesPreserveWideAndSubnormalStatistics()
    {
        foreach (
            var prices in new[]
            {
                new[]
                {
                    0d,
                    double.Epsilon,
                    2 * double.Epsilon,
                    4 * double.Epsilon,
                    8 * double.Epsilon,
                },
                new[]
                {
                    0d,
                    double.MaxValue / 4,
                    double.MaxValue / 2,
                    double.MaxValue * .75,
                    double.MaxValue,
                },
            }
        )
        foreach (var period in new[] { 3, int.MaxValue })
            ComparisonVerifier.Compare(
                CurvatureComparison.Reference(prices, period, false),
                CurvatureComparison.Owned(prices, period),
                "wide composed statistics",
                OoplesFinance.StockIndicators.Validation.IndicatorErrorBudget.Exact
            );
    }

    [Fact]
    public void NativeFlatWindowsRetainCorrelationWhileOwnedPresenceClears()
    {
        var data = CurvatureComparison.Fixture();
        var native = CurvatureComparison.Pair.Competitor(data, 3).Outputs["RSquared"];
        var owned = CurvatureComparison.Pair.Ooples(data, 3).Outputs["RSquared"];
        Assert.True(native.Present![10]);
        Assert.False(owned.Present![10]);
        Assert.True(double.IsFinite(native.Values[10]));
    }

    [Fact]
    public void NativeResetLeavesInnerSlopeHistoryAndWarmupMetadataDiffersFromHotFlag()
    {
        var native = new QuanTAlib.Curvature(3);
        foreach (var value in new[] { 1d, 2, 4 })
            native.Calc(new QuanTAlib.TValue(value, true, false));
        Assert.Equal(5, native.WarmupPeriod);
        Assert.True(native.IsHot);
        native.Init();
        native.Calc(new QuanTAlib.TValue(8, true, false));
        Assert.Equal(3, native.Calc(new QuanTAlib.TValue(16, true, false)).Value);
        var fresh = new QuanTAlib.Curvature(3);
        fresh.Calc(new QuanTAlib.TValue(8, true, false));
        Assert.Equal(8, fresh.Calc(new QuanTAlib.TValue(16, true, false)).Value);
        Assert.Throws<ArgumentOutOfRangeException>(() => new QuanTAlib.Curvature(2));
    }

    [Fact]
    public void NativeSourceAndRevisionRoutesIncludeAllStatistics()
    {
        var source = new QuanTAlib.TSeries();
        var subscribed = new QuanTAlib.Curvature(source, 3);
        var direct = new QuanTAlib.Curvature(3);
        var prices = CurvatureComparison.Fixture().Closes;
        foreach (var value in prices)
        {
            var input = new QuanTAlib.TValue(value, true, false);
            direct.Calc(input);
            source.Add(input);
            Assert.Equal(Fields(direct), Fields(subscribed));
        }
        direct.Calc(new QuanTAlib.TValue(7, false, false));
        var fresh = new QuanTAlib.Curvature(3);
        foreach (var value in prices.SkipLast(1).Append(7))
            fresh.Calc(new QuanTAlib.TValue(value, true, false));
        Assert.Equal(Fields(fresh), Fields(direct));
    }

    private static double?[] Fields(QuanTAlib.Curvature value) =>
        [value.Value, value.Intercept, value.StdDev, value.RSquared, value.Line];

    [Fact]
    public void EachOutputPresenceAndFirstStageCorruptionIsDetected()
    {
        var pair = CurvatureComparison.Pair;
        var data = CurvatureComparison.Fixture();
        foreach (var name in CurvatureComparison.Names)
        foreach (var native in new[] { false, true })
        foreach (var presence in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData input, int period)
            {
                var result = native ? pair.Competitor(input, period) : pair.Ooples(input, period);
                var output = result.Outputs[name];
                if (presence)
                    output.Present![^1] = false;
                else
                    output.Values[^1] += 1;
                return result;
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    data,
                    3
                )
            );
        }
        var corrupted = pair with
        {
            Library = (input, period) =>
                CurvatureComparison.Owned(input.Closes.Select(v => 2 * v).ToArray(), period),
        };
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Check(corrupted, data, 3)
        );
    }
}

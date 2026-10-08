using System.Numerics;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class WindowModeComparisonTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(20)]
    public async Task IndependentRationalContractCoversLifecycleAndExtremes(int period)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(WindowMode),
                "rolling mode",
                () => new WindowMode(period)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void WarmupMeanTiedModesAndExpirationHaveHandCalculatedOutputs()
    {
        Assert.Equal(3, new WindowMode(4).WarmupBars);
        var pair = WindowModeComparison.Pair;
        var result = pair.Ooples(WindowModeComparison.Fixture(), 3).Outputs["Value"].Values;
        Assert.Equal(new[] { 1d, 1, 1, 13d / 3, 3, 3, 9, 9, -1, -1, -1, 11d / 3 }, result);
        var tied = pair.Ooples(CompetitorData.FromCloses([1, 1, 3, 3, 9, 9]), 4)
            .Outputs["Value"]
            .Values;
        Assert.Equal(new[] { 1d, 1, 5d / 3, 2, 3, 6 }, tied);
        var inputs = WindowModeComparison.Fixture();
        Assert.Equal(inputs.Closes, pair.Ooples(inputs, 1).Outputs["Value"].Values);
    }

    [Fact]
    public void AdjacentValuesStayDistinctAndNativeStartupRetainsSimdRounding()
    {
        var pair = WindowModeComparison.Pair;
        var adjacent = WindowModeComparison.AdjacentFixture();
        ComparisonVerifier.Check(pair, adjacent, 3);
        var result = pair.Ooples(adjacent, 3).Outputs["Value"].Values;
        Assert.Equal(.1, result[2]);
        Assert.Equal(Math.BitIncrement(.1), result[3]);
        var lanes = WindowModeComparison.LaneFixture();
        ComparisonVerifier.Check(pair, lanes, 9);
        Assert.Equal(.75, pair.Ooples(lanes, 9).Outputs["Value"].Values[^1]);
        var expected =
            Vector<double>.Count == 4 ? .5
            : Vector<double>.Count == 2 ? .75
            : .625;
        Assert.Equal(expected, pair.Competitor(lanes, 9).Outputs["Value"].Values[^1]);
    }

    [Fact]
    public async Task ConvexMeanOfTiesAvoidsNativeOverflowAndMaximumAllocation()
    {
        var max = double.MaxValue;
        var owned = await VolumePriceComparisonTests.Run(
            new WindowMode(3),
            (max, max, max, 0),
            (-max, -max, -max, 0),
            (-max / 2, -max / 2, -max / 2, 0)
        );
        Assert.Equal(-max / 6, owned[0][2]);
        var native = new QuanTAlib.Mode(3);
        foreach (var value in new[] { max, -max, -max / 2 })
            native.Calc(new QuanTAlib.TValue(value, true, false));
        Assert.True(double.IsNegativeInfinity(native.Value));
        var lazy = await VolumePriceComparisonTests.Run(
            new WindowMode(int.MaxValue),
            (max, max, max, 0),
            (-max, -max, -max, 0),
            (1, 1, 1, 0)
        );
        Assert.Equal(new[] { max, 0, 1d / 3 }, lazy[0]);
        var tiny = await VolumePriceComparisonTests.Run(
            new WindowMode(2),
            (double.Epsilon, double.Epsilon, double.Epsilon, 0),
            (double.Epsilon, double.Epsilon, double.Epsilon, 0)
        );
        Assert.Equal(new[] { double.Epsilon, double.Epsilon }, tiny[0]);
    }

    [Fact]
    public void NativeSourceRevisionAndStaleBufferAfterResetAreExplicit()
    {
        var source = new QuanTAlib.TSeries();
        var subscribed = new QuanTAlib.Mode(source, 3);
        var direct = new QuanTAlib.Mode(3);
        foreach (var value in new[] { 1d, 1, 3 })
        {
            var input = new QuanTAlib.TValue(value, true, false);
            var expected = direct.Calc(input).Value;
            source.Add(input);
            Assert.Equal(expected, subscribed.Value);
        }
        Assert.Equal(1, direct.Calc(new QuanTAlib.TValue(7, false, false)).Value);
        direct.Calc(new QuanTAlib.TValue(3, false, false));
        direct.Init();
        Assert.Equal(14d / 3, direct.Calc(new QuanTAlib.TValue(10, true, false)).Value);
        Assert.Throws<ArgumentOutOfRangeException>(() => new QuanTAlib.Mode(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowMode(0));
    }

    [Fact]
    public void FullTrajectoriesAndCorruptValuesAreIndependentlyChecked()
    {
        var pair = WindowModeComparison.Pair;
        foreach (var period in new[] { 1, 2, 3, 4, 20 })
        {
            ComparisonVerifier.Check(pair, WindowModeComparison.Fixture(), period);
            ComparisonVerifier.Check(pair, WindowModeComparison.AdjacentFixture(), period);
        }
        foreach (var native in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData data, int period)
            {
                var result = native ? pair.Competitor(data, period) : pair.Ooples(data, period);
                result.Outputs["Value"].Values[^1] += 1;
                return result;
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    WindowModeComparison.Fixture(),
                    3
                )
            );
        }
    }
}

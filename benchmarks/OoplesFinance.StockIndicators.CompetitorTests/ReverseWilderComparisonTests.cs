using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class ReverseWilderComparisonTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(20)]
    [InlineData(int.MaxValue)]
    public async Task ExactRecurrenceAndLifecycleAreIndependentlyVerified(int period)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(ReverseWilderAverage),
                "reverse Wilder",
                () => new ReverseWilderAverage(period)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void PinnedRecurrenceIsAnExtrapolatorAndPeriodOneIsNotIdentity()
    {
        var data = CompetitorData.FromCloses([3, 12, -6, 9, 18]);
        foreach (var native in new[] { false, true })
        {
            var pair = ReverseWilderComparison.Pair;
            var result = native ? pair.Competitor(data, 3) : pair.Ooples(data, 3);
            Assert.Equal(new[] { 3d, 7.5, 3, 1 }, result.Outputs["Value"].Values.Take(4));
            Assert.Equal(-14d / 3, result.Outputs["Value"].Values[4], 14);
            var identity = native ? pair.Competitor(data, 1) : pair.Ooples(data, 1);
            Assert.Equal(new[] { 3d, -6, -6, -21, -60 }, identity.Outputs["Value"].Values);
        }
    }

    [Fact]
    public async Task StartupUsesPreviouslyRoundedMeanRatherThanExactPrefixSum()
    {
        var data = ReverseWilderComparison.RoundedSeedFixture();
        var pair = ReverseWilderComparison.Pair;
        ComparisonVerifier.Check(pair, data, 3);
        Assert.Equal(0, pair.Ooples(data, 3).Outputs["Value"].Values[2]);
        var conventional = new WilderMovingAverage(3);
        using var run = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(data.IndicatorBars))
            .ConfigureIndicators(conventional)
            .BuildAsync();
        Assert.Equal(1d / 3, run[conventional.Outputs[0]].ToArray()[2]);
    }

    [Fact]
    public async Task CompleteUpdatesNormalizeWideTermsButRejectFinalOverflow()
    {
        var max = double.MaxValue;
        var result = await VolumePriceComparisonTests.Run(
            new ReverseWilderAverage(2),
            (max, max, max, 0),
            (max, max, max, 0),
            (max, max, max, 0)
        );
        Assert.Equal(new[] { max, max, max }, result[0]);
        var tiny = await VolumePriceComparisonTests.Run(
            new ReverseWilderAverage(2),
            (double.Epsilon, double.Epsilon, double.Epsilon, 0),
            (double.Epsilon, double.Epsilon, double.Epsilon, 0),
            (double.Epsilon, double.Epsilon, double.Epsilon, 0)
        );
        Assert.Equal(new[] { double.Epsilon, double.Epsilon, double.Epsilon }, tiny[0]);
        await Assert.ThrowsAsync<IndicatorOutputException>(() =>
            VolumePriceComparisonTests.Run(
                new ReverseWilderAverage(1),
                (max, max, max, 0),
                (-max, -max, -max, 0)
            )
        );
        var lazy = await VolumePriceComparisonTests.Run(
            new ReverseWilderAverage(int.MaxValue),
            (max, max, max, 0),
            (max, max, max, 0)
        );
        Assert.Equal(new[] { max, max }, lazy[0]);
        var native = new QuanTAlib.Rma(2);
        native.Calc(new QuanTAlib.TValue(max, true, false));
        Assert.True(
            double.IsPositiveInfinity(native.Calc(new QuanTAlib.TValue(max, true, false)).Value)
        );
    }

    [Fact]
    public void NativeSourceRevisionAndResetRoutesAgreeWithFreshReplay()
    {
        var source = new QuanTAlib.TSeries();
        var subscribed = new QuanTAlib.Rma(source, 3);
        var direct = new QuanTAlib.Rma(3);
        foreach (var value in ReverseWilderComparison.Fixture().Closes)
        {
            var input = new QuanTAlib.TValue(value, true, false);
            var expected = direct.Calc(input).Value;
            source.Add(input);
            Assert.Equal(expected, subscribed.Value);
        }
        var revised = direct.Calc(new QuanTAlib.TValue(7, false, false)).Value;
        var fresh = new QuanTAlib.Rma(3);
        foreach (var value in ReverseWilderComparison.Fixture().Closes.SkipLast(1).Append(7))
            fresh.Calc(new QuanTAlib.TValue(value, true, false));
        Assert.Equal(fresh.Value, revised);
        direct.Init();
        Assert.Equal(7, direct.Calc(new QuanTAlib.TValue(7, true, false)).Value);
        Assert.Throws<ArgumentException>(() => new QuanTAlib.Rma(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReverseWilderAverage(0));
        var maximum = new QuanTAlib.Rma(int.MaxValue);
        Assert.Equal(-2, maximum.WarmupPeriod);
        Assert.Equal(3, maximum.Calc(new QuanTAlib.TValue(3, true, false)).Value);
        Assert.Equal(7.5, maximum.Calc(new QuanTAlib.TValue(12, true, false)).Value);
    }

    [Fact]
    public void IndependentReferencesDetectWrongDirectionSeedAndAlignment()
    {
        var pair = ReverseWilderComparison.Pair;
        foreach (var period in new[] { 1, 2, 3, 20 })
        {
            ComparisonVerifier.Check(pair, ReverseWilderComparison.Fixture(), period);
            ComparisonVerifier.Check(pair, ReverseWilderComparison.RoundedSeedFixture(), period);
            ComparisonVerifier.Check(pair, CompetitorData.FromCloses([5]), period);
        }
        foreach (var native in new[] { false, true })
        foreach (var alignment in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData data, int period)
            {
                var series = native ? pair.Competitor(data, period) : pair.Ooples(data, period);
                var values = series.Outputs["Value"].Values;
                if (!alignment)
                    values[period] += 1;
                return new(alignment ? period - 1 : 0, values);
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    ReverseWilderComparison.Fixture(),
                    3
                )
            );
        }
    }
}

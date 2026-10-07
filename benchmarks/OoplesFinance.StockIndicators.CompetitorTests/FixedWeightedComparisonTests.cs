using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class FixedWeightedComparisonTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(20)]
    public async Task ExactLinearWeightsHaveIndependentLifecycleContracts(int period)
    {
        var report = await IndicatorValidation.ValidateAsync(
            new IndicatorValidationCase(
                typeof(FixedPeriodWma),
                "fixed-period linear weights",
                () => new FixedPeriodWma(period)
            )
        );
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void StartupRetainsPeriodWeightsAndDoubleSmoothingRoundsEachStage()
    {
        Assert.Equal(2, new FixedPeriodWma(3).WarmupBars);
        var data = CompetitorData.FromCloses([3, 12, -6, 9]);
        var single = ComparisonPairs.Get("QuanTAlib.Wma");
        var twice = ComparisonPairs.Get("QuanTAlib.Dwma");
        Assert.Equal(new[] { 3d, 9, 0, 4 }, single.Ooples(data, 2).Outputs["Value"].Values);
        Assert.Equal(new[] { 3d, 7, 3, 8d / 3 }, twice.Ooples(data, 2).Outputs["Value"].Values);
        Assert.Equal(42d / 5, single.Ooples(data, 3).Outputs["Value"].Values[1]);
        Assert.NotEqual(9, single.Ooples(data, 3).Outputs["Value"].Values[1]);
        var rounding = twice.Ooples(CompetitorData.FromCloses([1, 3]), 2).Outputs["Value"].Values;
        Assert.Equal(Math.BitIncrement(17d / 9), rounding[1]);
        foreach (var pair in FixedWeightedComparison.Pairs)
        {
            Assert.Equal(0, pair.Ooples(data, 20).Outputs["Value"].FirstValid);
            Assert.Equal(data.Closes, pair.Ooples(data, 1).Outputs["Value"].Values);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WideWeightsAndMaximumPeriodsStayLazyAndFinite(bool twice)
    {
        foreach (var period in new[] { 2, int.MaxValue })
        foreach (var value in new[] { double.MaxValue, -double.MaxValue, double.Epsilon })
        {
            var indicator = new FixedPeriodWma(period);
            if (twice)
                indicator.Of(new FixedPeriodWma(period));
            var result = await VolumePriceComparisonTests.Run(
                indicator,
                (value, value, value, 0),
                (value, value, value, 0),
                (value, value, value, 0)
            );
            Assert.Equal(new[] { value, value, value }, result[0]);
        }
    }

    [Fact]
    public void IndependentReferencesCoverEveryStartupAndEvictionValue()
    {
        foreach (var pair in FixedWeightedComparison.Pairs)
        foreach (var period in new[] { 1, 2, 3, 20, 65536 })
        {
            ComparisonVerifier.Check(pair, FixedWeightedComparison.Fixture(), period);
            ComparisonVerifier.Check(pair, CompetitorData.FromCloses([7]), period);
        }
    }

    [Fact]
    public void NativeSourceRevisionAndHiddenResetBehaviorAreExplicit()
    {
        foreach (var twice in new[] { false, true })
        {
            var source = new QuanTAlib.TSeries();
            QuanTAlib.AbstractBase subscribed = twice
                ? new QuanTAlib.Dwma(source, 3)
                : new QuanTAlib.Wma(source, 3);
            QuanTAlib.AbstractBase direct = twice ? new QuanTAlib.Dwma(3) : new QuanTAlib.Wma(3);
            foreach (var value in new[] { 1d, 2, 3 })
            {
                var input = new QuanTAlib.TValue(value, true, false);
                var expected = direct.Calc(input).Value;
                source.Add(input);
                Assert.Equal(expected, subscribed.Value);
            }
            var revised = direct.Calc(new QuanTAlib.TValue(7, false, false)).Value;
            QuanTAlib.AbstractBase replay = twice ? new QuanTAlib.Dwma(3) : new QuanTAlib.Wma(3);
            foreach (var value in new[] { 1d, 2, 7 })
                replay.Calc(new QuanTAlib.TValue(value, true, false));
            Assert.Equal(replay.Value, revised);
            direct.Init();
            Assert.NotEqual(10, direct.Calc(new QuanTAlib.TValue(10, true, false)).Value);
        }
        Assert.Throws<ArgumentException>(() => new QuanTAlib.Wma(0));
        Assert.Throws<ArgumentException>(() => new QuanTAlib.Dwma(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FixedPeriodWma(0));
    }

    [Fact]
    public void WrongWeightDirectionStageCountAndStartupAlignmentAreDetected()
    {
        var data = FixedWeightedComparison.Fixture();
        foreach (var pair in FixedWeightedComparison.Pairs)
        foreach (var native in new[] { false, true })
        foreach (var startup in new[] { false, true })
        {
            ComparisonSeries Bad(CompetitorData d, int p)
            {
                var values = (native ? pair.Competitor(d, p) : pair.Ooples(d, p))
                    .Outputs["Value"]
                    .Values;
                if (!startup)
                    values[^1] += 1;
                return new(startup ? p - 1 : 0, values);
            }
            Assert.Throws<InvalidOperationException>(() =>
                ComparisonVerifier.Check(
                    native ? pair with { Competitor = Bad } : pair with { Library = Bad },
                    data,
                    3
                )
            );
        }
        var doublePair = ComparisonPairs.Get("QuanTAlib.Dwma");
        var singlePair = ComparisonPairs.Get("QuanTAlib.Wma");
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Check(doublePair with { Library = singlePair.Ooples }, data, 3)
        );
    }
}

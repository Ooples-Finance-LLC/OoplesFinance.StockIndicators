using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class MedianAdaptiveComparisonTests
{
    [Fact]
    public void GoldensSeparateRawSmoothedAndAdaptiveStartup()
    {
        var data = CompetitorData.FromCloses([1, 2, 3, 4, 5, 6]);
        Assert.Equal(
            new[] { 1d, 2, 3, 1.25, 2.375, 3.4375 },
            MedianAdaptiveSnapshot.Calculate(data.IndicatorBars, 1)
        );
        Assert.Equal(
            new[] { 1d, 2, 3, 2.5, 1.75, 3.125 },
            MedianAdaptiveSnapshot.Calculate(data.IndicatorBars, 2)
        );
        foreach (var period in new[] { 1, 2, 3, 9 })
            ComparisonVerifier.Check(MedianAdaptiveComparison.Pair(), data, period);
    }

    [Fact]
    public void SignedMedianZeroMedianAndThresholdBoundariesMatchIndependentSelection()
    {
        foreach (
            var threshold in new[]
            {
                -.2,
                0,
                .002,
                Math.BitDecrement(.2),
                .2,
                Math.BitIncrement(.2),
                1,
            }
        )
        foreach (
            var values in new[]
            {
                Enumerable.Range(0, 25).Select(i => -20d - i).ToArray(),
                new double[25],
                Enumerable.Range(0, 25).Select(i => i % 3 == 0 ? -10d : 20d).ToArray(),
            }
        )
        {
            var data = CompetitorData.FromCloses(values);
            var actual = MedianAdaptiveSnapshot.Calculate(data.IndicatorBars, 6, threshold);
            Assert.Equal(MedianAdaptiveComparison.Reference(values, 6, threshold, false), actual);
            ComparisonVerifier.Check(MedianAdaptiveComparison.Pair(threshold), data, 6);
        }
    }

    [Fact]
    public void MaximumPeriodsAndInvalidArgumentsHaveBoundedStartup()
    {
        Assert.Throws<ArgumentNullException>(() => MedianAdaptiveSnapshot.Calculate(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => MedianAdaptiveSnapshot.Calculate([], 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MedianAdaptiveSnapshot.Calculate([], threshold: double.NaN)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MedianAdaptiveSnapshot.Calculate([
                new Bar(DateTime.UnixEpoch, 1, 1, 1, double.PositiveInfinity, 0),
            ])
        );
        var data = CompetitorData.Create(12);
        Assert.Equal(
            MedianAdaptiveComparison.Reference(data.Closes, int.MaxValue, .002, false),
            MedianAdaptiveSnapshot.Calculate(data.IndicatorBars, int.MaxValue)
        );
    }

    [Fact]
    public void WideTinyInputsAndIndependentCallsRetainFiniteValues()
    {
        foreach (var scale in new[] { double.MaxValue, double.Epsilon, 1e-200 })
        {
            var prices = Enumerable.Range(0, 40).Select(i => i % 3 == 0 ? -scale : scale).ToArray();
            var bars = prices
                .Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 0))
                .ToArray();
            var values = MedianAdaptiveSnapshot.Calculate(bars, 5);
            Assert.Equal(MedianAdaptiveComparison.Reference(prices, 5, .002, false), values);
            Assert.All(values, v => Assert.True(double.IsFinite(v)));
            Assert.Equal(values, MedianAdaptiveSnapshot.Calculate(bars, 5));
            Assert.Equal(
                values.Take(20),
                MedianAdaptiveSnapshot.Calculate(bars.Take(20).ToArray(), 5)
            );
        }
    }

    [Fact]
    public void OutputAndThresholdMutationsFailExactComparison()
    {
        var data = CompetitorData.Create(100);
        var expected = new ComparisonSeries(
            0,
            MedianAdaptiveSnapshot.Calculate(data.IndicatorBars, 9).ToArray()
        );
        var changed = MedianAdaptiveSnapshot.Calculate(data.IndicatorBars, 9).ToArray();
        changed[^1] += 1;
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Compare(
                expected,
                new(0, changed),
                "median adaptive value",
                IndicatorErrorBudget.Exact
            )
        );
        Assert.Throws<InvalidOperationException>(() =>
            ComparisonVerifier.Compare(
                expected,
                new(0, MedianAdaptiveSnapshot.Calculate(data.IndicatorBars, 9, .3).ToArray()),
                "median adaptive threshold",
                IndicatorErrorBudget.Exact
            )
        );
    }
}

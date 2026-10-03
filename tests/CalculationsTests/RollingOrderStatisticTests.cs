using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.CalculationsTests;

/// <summary>
/// A preview leaves the order statistic exactly as it found it, whatever the pending value is.
/// </summary>
public sealed class RollingOrderStatisticTests
{
    /// <summary>Above the small-window threshold, so the order-statistic tree is the path under test.</summary>
    private const int Length = RollingWindowSettings.SmallWindowThreshold + 8;

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(Length)]
    public void PendingStrictRankMatchesInsertionWithoutMutating(int length)
    {
        using var order = new RollingOrderStatistic(length);
        var history = new List<double>();
        for (var i = 0; i < length * 2 + 5; i++)
        {
            var pending = (double)(i % 7);
            var kept = history.Skip(Math.Max(0, history.Count - length + 1)).ToArray();
            var before = order.CountLessThan(pending);
            Assert.Equal(kept.Count(v => v < pending), order.PreviewCountLessThan(pending));
            Assert.Equal(before, order.CountLessThan(pending));
            order.Add(pending);
            history.Add(pending);
            Assert.Equal(kept.Count(v => v < pending), order.CountLessThan(pending));
        }
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(17.5)]
    public void APreviewLeavesNoTrace(double pending)
    {
        using var previewed = new RollingOrderStatistic(Length);
        using var twin = new RollingOrderStatistic(Length);
        for (var i = 0; i < Length + 5; i++)
        {
            previewed.Add(i);
            twin.Add(i);
        }

        // A NaN went right on insert and was never found on removal, so this left a node in the tree and every
        // later percentile counted one value too many.
        previewed.PercentileNearestRank(50, pending);

        for (var i = 0; i < 10; i++)
        {
            previewed.Add(100 + i);
            twin.Add(100 + i);
            foreach (var percentile in new[] { 25d, 50d, 75d })
            {
                previewed.PercentileNearestRank(percentile).Should().Be(twin.PercentileNearestRank(percentile),
                    $"after add {i}, the {percentile}th percentile is the twin's");
            }
        }
    }
}

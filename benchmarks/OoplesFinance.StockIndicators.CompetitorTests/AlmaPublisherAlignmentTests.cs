using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class AlmaPublisherAlignmentTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void AlignedProjectionPreservesFullValuesPresenceAndOwnership(bool fullWindow)
    {
        foreach (int count in new[] { 0, 1, 19, 20, 148, 513 })
        {
            var data = CompetitorData.FromCloses(Enumerable.Range(0, count)
                .Select(i => i % 9 == 0 ? -0d : (i * 13 % 101 - 50) / 8d).ToArray());
            var expected = AlmaComparison.Reference(data.Closes, 20, fullWindow, .85, 6, false).Outputs["Value"];
            var actual = AlmaComparison.Owned(data.IndicatorBars, 20, fullWindow).Outputs["Value"];
            Assert.Equal(0, actual.FirstValid);
            Assert.Equal(expected.Present, actual.Present);
            Assert.Equal(expected.Values.Select(BitConverter.DoubleToInt64Bits),
                actual.Values.Select(BitConverter.DoubleToInt64Bits));
            // Owned disposes its run before returning. Input lifetime and later
            // mutation must not change either projected comparison column.
            Array.Clear(data.IndicatorBars); Array.Clear(data.Closes);
            Assert.Equal(expected.Present, actual.Present);
            Assert.Equal(expected.Values.Select(BitConverter.DoubleToInt64Bits),
                actual.Values.Select(BitConverter.DoubleToInt64Bits));
        }
    }
}

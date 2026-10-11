using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class WindowExtremeDequeTests
{
    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void RingMatchesIndependentWindowScanAcrossGrowthWrapAndReset(bool maximum, bool newestTie)
    {
        double[] edge = [-0d, 0d, double.Epsilon, -double.Epsilon, double.MaxValue, -double.MaxValue, 1, 1, -1];
        var values = Enumerable.Range(0, 300).Select(i => i < 90 ? (double)i : i < 180 ? -i : edge[i % edge.Length]).ToArray();
        foreach (long period in new long[] { 1, 2, 3, 4, 5, 17, 65, int.MaxValue, (long)int.MaxValue + 1 })
        {
            var deque = new WindowExtremeDeque(period, maximum, newestTie);
            foreach (int count in new[] { values.Length, 2, values.Length })
            {
                deque.Reset();
                for (int i = 0; i < count; i++)
                {
                    deque.Add(values[i]);
                    int best = (int)Math.Max(0, i - period + 1);
                    for (int j = best + 1; j <= i; j++)
                    {
                        bool replace = maximum
                            ? newestTie ? values[j] >= values[best] : values[j] > values[best]
                            : newestTie ? values[j] <= values[best] : values[j] < values[best];
                        if (replace) best = j;
                    }
                    Assert.Equal(best, deque.Index);
                    Assert.Equal(BitConverter.DoubleToInt64Bits(values[best]), BitConverter.DoubleToInt64Bits(deque.Value));
                }
            }
        }
    }

    [Fact]
    public void MidpointFastPathAndFallbackRoundOnceAtExponentBoundaries()
    {
        double[] values = [0, -0d, double.Epsilon, -double.Epsilon, 3 * double.Epsilon,
            double.MaxValue, -double.MaxValue, Math.ScaleB(1, -1022), -Math.ScaleB(1, -1022),
            Math.BitIncrement(Math.ScaleB(1, -1022)), Math.ScaleB(1, -1021), 1, Math.BitIncrement(1d), -1];
        var state = (IIndicatorState)new WindowRangeMidpoint(1, true).CreateState();
        foreach (double low in values)
        foreach (double high in values)
        {
            var bar = new Bar(default, 0, high, low, 0, 1);
            double expected = ((ReferenceFraction.FromDouble(low) + ReferenceFraction.FromDouble(high)) / new ReferenceFraction(2)).ToDouble();
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(state.Update(in bar)));
        }
    }

    [Fact]
    public void FullWindowUpdatesAndResetDoNotAllocateNodes()
    {
        var deque = new WindowExtremeDeque(31, false);
        for (int i = 0; i < 100; i++) deque.Add(i);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 100; i < 1100; i++) deque.Add(i);
        deque.Reset();
        for (int i = 0; i < 100; i++) deque.Add(i);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.Equal(69, deque.Index);
    }
}

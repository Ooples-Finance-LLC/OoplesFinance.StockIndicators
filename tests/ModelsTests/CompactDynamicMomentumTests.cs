using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class CompactDynamicMomentumTests(Xunit.Abstractions.ITestOutputHelper log)
{
    [Fact]
    public void ExactPeriodSelectionPreservesIntegerBoundariesAndExtremeRatios()
    {
        foreach (int baseline in new[] { 1, 14, int.MaxValue })
        foreach (int maximum in new[] { 5, 30, int.MaxValue })
        foreach (int minimum in new[] { 1, maximum })
        {
            var ratios = new List<double> { double.Epsilon, double.MaxValue, 1e-200, 1e200 };
            foreach (int divisor in new[] { 1, 2, 5, 30, int.MaxValue })
            {
                double boundary = (double)baseline / divisor;
                ratios.Add(Math.BitDecrement(boundary));
                ratios.Add(boundary);
                ratios.Add(Math.BitIncrement(boundary));
            }
            foreach (double ratio in ratios)
            {
                BigInteger n = (BigInteger)baseline << 1074, d = ExactVarianceWindow.Units(ratio);
                int expected = n >= d * maximum ? maximum : n <= d * minimum ? minimum : (int)(n / d);
                Assert.Equal(expected, DynamicMomentumPeriod.SelectExact(ratio, baseline, minimum, maximum));
            }
        }
    }

    [Fact]
    public void OrdinarySnapshotAllocationIsBoundedIncludingCachedStrengthTrajectory()
    {
        var values = Enumerable.Range(0, 1000).Select(i => (double?)(100 + (i * 13 % 101) / 8d)).ToArray();
        for (int i = 0; i < 3; i++) DynamicMomentumSnapshot.FromValues(values, upperLimit: 5, lowerLimit: 5);
        long before = GC.GetAllocatedBytesForCurrentThread();
        var result = DynamicMomentumSnapshot.FromValues(values, upperLimit: 5, lowerLimit: 5);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        log.WriteLine($"Dynamic momentum snapshot, 1000 bars, one cached RSI period: {allocated} allocated bytes.");
        Assert.Equal(1000, result.Count);
        Assert.InRange(allocated, 1, 1_000_000);
        GC.KeepAlive(result);
    }
}

using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using ReturnKey = OoplesFinance.StockIndicators.Helpers.ReturnOrderStatistic.ReturnKey;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class CompactConnorsTests(Xunit.Abstractions.ITestOutputHelper log)
{
    [Fact]
    public void KeysMatchExactCrossProductsIncludingOverflowAndFalseTies()
    {
        double[] values = [0, -0d, 1, -1, Math.BitIncrement(1d), Math.BitDecrement(1d),
            double.Epsilon, -double.Epsilon, double.MaxValue, -double.MaxValue, 1e-200, 1e200];
        (BigInteger N, BigInteger D) Ratio(double current, double previous) => previous == 0
            ? (BigInteger.One, BigInteger.One)
            : (ExactVarianceWindow.Units(previous < 0 ? -current : current),
                ExactVarianceWindow.Units(Math.Abs(previous)));
        foreach (double a in values)
        foreach (double b in values)
        foreach (double c in values)
        {
            var left = Ratio(a, b);
            var right = Ratio(c, a);
            int expected = (left.N * right.D - right.N * left.D).Sign;
            Assert.Equal(expected, new ReturnKey(a, b).CompareTo(new ReturnKey(c, a)));
        }
        Assert.Equal(0, new ReturnKey(2, 1).CompareTo(new ReturnKey(4, 2)));
    }

    [Fact]
    public void OrdinaryKeyComparisonsAllocateNothing()
    {
        var keys = Enumerable.Range(0, 100)
            .Select(i => new ReturnKey(100 + i / 8d, 100 + (i * 13 % 101) / 8d)).ToArray();
        int Run()
        {
            int total = 0;
            for (int i = 0; i < 4000; i++) total += keys[i % 100].CompareTo(keys[(i * 7) % 100]);
            return total;
        }
        int expected = 0;
        for (int i = 0; i < 3; i++) expected = Run();
        long before = GC.GetAllocatedBytesForCurrentThread();
        int actual = Run();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SnapshotAllocationIsBoundedByResultsRatherThanRankCrossProducts()
    {
        var bars = Enumerable.Range(0, 1000).Select(i =>
        {
            double price = 100 + (i * 13 % 101) / 8d;
            return new Bar(default, price, price, price, price, 0);
        }).ToArray();
        for (int i = 0; i < 3; i++) ConnorsStrengthSnapshot.Calculate(bars);
        long before = GC.GetAllocatedBytesForCurrentThread();
        var result = ConnorsStrengthSnapshot.Calculate(bars);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        log.WriteLine($"Connors snapshot, 1000 bars, default periods: {allocated} allocated bytes.");
        Assert.Equal(1000, result.Count);
        // Includes result records, both RSI states and lazily grown history.
        // The prior padded rank comparisons alone consumed tens of megabytes.
        Assert.InRange(allocated, 1, 250_000);
        GC.KeepAlive(result);
    }
}

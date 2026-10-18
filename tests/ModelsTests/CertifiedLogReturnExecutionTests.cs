using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class CertifiedLogReturnExecutionTests
{
    [Fact]
    public void UnresolvedNearEqualPricesRetainAdaptiveFallback()
    {
        double current = Math.BitIncrement(1d);
        Assert.False(CertifiedLogReturn.TryFixed(new(current), new(1d), out _));
        Bits(ReferenceFraction.FromDouble(current).LogToDouble(), CertifiedLogReturn.Of(current, 1d));
    }

    [Fact]
    public void FixedIntervalsMatchIndependentLogarithmAcrossExponentAndRatioBoundaries()
    {
        double[] values = [double.Epsilon, 2 * double.Epsilon, Math.ScaleB(1, -1022),
            Math.BitDecrement(.75), .75, Math.BitIncrement(.75),
            Math.BitDecrement(1d), 1d, Math.BitIncrement(1d),
            Math.BitDecrement(1.5), 1.5, Math.BitIncrement(1.5), 2d,
            Math.ScaleB(1, 512), double.MaxValue];
        foreach (double current in values)
        foreach (double previous in values)
        {
            double expected = (ReferenceFraction.FromDouble(current)
                / ReferenceFraction.FromDouble(previous)).LogToDouble();
            Bits(expected, CertifiedLogReturn.Of(current, previous));
            Bits(expected, CertifiedLogReturn.Of(-current, -previous));
            if (CertifiedLogReturn.TryFixed(new(current), new(previous), out var fast))
                Bits(expected, fast);
        }
    }

    [Fact]
    public void RandomSignificandsAndAdjacentPricesRetainExactRounding()
    {
        var random = new Random(18471);
        for (int i = 0; i < 160; i++)
        {
            double previous = Math.ScaleB(1 + random.NextDouble(), random.Next(-1000, 1001));
            double current = i % 2 == 0 ? Math.BitIncrement(previous)
                : Math.ScaleB(1 + random.NextDouble(), random.Next(-1000, 1001));
            double expected = (ReferenceFraction.FromDouble(current)
                / ReferenceFraction.FromDouble(previous)).LogToDouble();
            Bits(expected, CertifiedLogReturn.Of(current, previous));
        }
    }

    [Fact]
    public void CertifiedOrdinaryReturnsAllocateNoPerSampleObjects()
    {
        var prices = Enumerable.Range(0, 101).Select(i => 10 + (i * 13 % 101) / 8d).ToArray();
        double result = 0;
        for (int i = 1; i <= 1000; i++)
            result += CertifiedLogReturn.Of(prices[i % prices.Length], prices[(i - 1) % prices.Length]);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 1; i <= 4000; i++)
            result += CertifiedLogReturn.Of(prices[i % prices.Length], prices[(i - 1) % prices.Length]);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(double.IsFinite(result));
    }

    private static void Bits(double expected, double actual) =>
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
}

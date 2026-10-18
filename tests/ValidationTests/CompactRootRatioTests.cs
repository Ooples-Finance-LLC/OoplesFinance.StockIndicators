using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class CompactRootRatioTests
{
    [Fact]
    public void CompactMidpointsRoundToEvenIncludingSubnormalsAndOverflow()
    {
        foreach (ulong root in new[] { 0UL, 1UL, 1UL << 52, (1UL << 52) + 1, (1UL << 53) - 1 })
        foreach (int grid in new[] { -1074, -1022, -53, 0, 970, 971 })
        foreach (int offset in new[] { -1, 0, 1 })
        {
            UInt128 midpoint = 2 * (UInt128)root + 1;
            UInt128 numerator = midpoint * midpoint;
            numerator = offset < 0 ? numerator - 1 : numerator + (uint)offset;
            Assert.True(ExactPopulationDeviation.TryCompactScaledRoot(numerator, 4, 2 * grid, out var actual));
            Bits(Expected(numerator, 4, 2 * grid), actual);
        }
    }

    [Fact]
    public void WideCompactMomentsMatchIndependentRootsAcrossExponents()
    {
        var random = new Random(46933);
        for (int i = 0; i < 1000; i++)
        {
            UInt128 numerator = ((UInt128)(ulong)random.NextInt64() << 64)
                | (ulong)random.NextInt64();
            numerator |= (UInt128)1 << 126;
            ulong denominator = (ulong)random.Next(1, 1 << 18);
            int power = random.Next(-1900, 1901);
            Assert.True(ExactPopulationDeviation.TryCompactScaledRoot(numerator, denominator, power, out var actual));
            Bits(Expected(numerator, denominator, power), actual);
        }
        Assert.False(ExactPopulationDeviation.TryCompactScaledRoot(UInt128.MaxValue, ulong.MaxValue, 0, out _));
        Assert.False(ExactPopulationDeviation.TryCompactScaledRoot(1, 0, 0, out _));
        Assert.False(ExactPopulationDeviation.TryCompactScaledRoot(1, 1, int.MaxValue, out _));
        Assert.False(ExactPopulationDeviation.TryCompactScaledRoot(1, 1, int.MinValue, out _));
    }

    [Fact]
    public void CompactPublicationAllocatesNoManagedObjects()
    {
        var numerator = ((UInt128)1 << 120) + 1234567;
        for (int i = 0; i < 1000; i++) ExactPopulationDeviation.TryCompactScaledRoot(numerator, 20, -120, out _);
        bool certified = true;
        double result = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 4000; i++)
            certified &= ExactPopulationDeviation.TryCompactScaledRoot(numerator, 20, -120, out result);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(certified);
        Bits(Expected(numerator, 20, -120), result);
    }

    private static double Expected(UInt128 numerator, ulong denominator, int power)
    {
        var value = new ReferenceFraction((BigInteger)numerator) / new ReferenceFraction(denominator);
        var scale = new ReferenceFraction(BigInteger.One << Math.Abs(power));
        return (power < 0 ? value / scale : value * scale).SqrtToDouble();
    }

    private static void Bits(double expected, double actual) =>
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
}

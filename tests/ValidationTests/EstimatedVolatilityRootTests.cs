using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class EstimatedVolatilityRootTests
{
    [Fact]
    public void IntegerRootsBracketSquaresAcrossEstimateAndFallbackBoundaries()
    {
        foreach (int width in new[] { 0, 1, 26, 31, 32, 52, 53, 54, 63 })
        foreach (int delta in new[] { -1, 0, 1 })
        {
            BigInteger root = (BigInteger.One << width) + delta;
            foreach (int offset in new[] { -1, 0, 1 })
            {
                BigInteger value = root * root + offset;
                if (value.Sign >= 0 && value.GetBitLength() <= 128) Check((UInt128)value);
            }
        }
        Check(UInt128.MaxValue);
        var random = new Random(20261110);
        for (int i = 0; i < 10000; i++)
            Check(((UInt128)(ulong)random.NextInt64() << 65)
                | ((UInt128)(ulong)random.NextInt64() << 1) | (uint)random.Next(2));
    }

    [Fact]
    public void EstimatedPublicationMatchesIndependentRationalMidpointsAndWideMoments()
    {
        foreach (ulong root in new[] { 0UL, 1UL, 1UL << 52, (1UL << 52) + 1, (1UL << 53) - 1 })
        foreach (int grid in new[] { -1074, -1022, -53, 0, 970, 971 })
        foreach (int offset in new[] { -1, 0, 1 })
        {
            UInt128 midpoint = 2 * (UInt128)root + 1;
            UInt128 numerator = midpoint * midpoint;
            numerator = offset < 0 ? numerator - 1 : numerator + (uint)offset;
            Publication(numerator, 4, 2 * grid);
        }
        var random = new Random(10061110);
        for (int i = 0; i < 1000; i++)
        {
            UInt128 numerator = ((UInt128)(ulong)random.NextInt64() << 64)
                | (ulong)random.NextInt64() | ((UInt128)1 << 126);
            Publication(numerator, (ulong)random.Next(1, 1 << 18), random.Next(-1900, 1901));
        }
        Assert.False(LogVolatilityRoot.TryRoot(UInt128.MaxValue, ulong.MaxValue, 0, out _));
        Assert.False(LogVolatilityRoot.TryRoot(1, 0, 0, out _));
        Assert.False(LogVolatilityRoot.TryRoot(1, 1, int.MinValue, out _));
        Assert.False(LogVolatilityRoot.TryRoot(1, 1, int.MaxValue, out _));
    }

    [Fact]
    public void EstimatedRootNeedsNoManagedScratch()
    {
        UInt128[] values = [0, 1, 2, ((UInt128)1 << 106) - 1, UInt128.MaxValue];
        UInt128 result = 0;
        for (int i = 0; i < 1000; i++) result |= LogVolatilityRoot.IntegerRoot(values[i % values.Length]);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 4000; i++) result |= LogVolatilityRoot.IntegerRoot(values[i % values.Length]);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(result != 0);
    }

    private static void Check(UInt128 value)
    {
        BigInteger root = (BigInteger)LogVolatilityRoot.IntegerRoot(value);
        Assert.True(root * root <= (BigInteger)value);
        Assert.True((root + 1) * (root + 1) > (BigInteger)value);
    }

    private static void Publication(UInt128 numerator, ulong denominator, int power)
    {
        var ratio = new ReferenceFraction((BigInteger)numerator) / new ReferenceFraction(denominator);
        var scale = new ReferenceFraction(BigInteger.One << Math.Abs(power));
        double expected = (power < 0 ? ratio / scale : ratio * scale).SqrtToDouble();
        Assert.True(LogVolatilityRoot.TryRoot(numerator, denominator, power, out var actual));
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
    }
}

using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class CompactIntegerRootTests
{
    [Fact]
    public void IntegerRootsBracketInputsAcrossWordAndPerfectSquareBoundaries()
    {
        foreach (int width in new[] { 0, 1, 31, 32, 53, 63, 64, 65, 105, 106, 127, 128, 129, 256 })
        foreach (int offset in new[] { -1, 0, 1 })
        {
            var value = (BigInteger.One << width) + offset;
            Check(value);
            var square = value * value;
            if (square > 0) Check(square - 1);
            Check(square);
            Check(square + 1);
        }
        var random = new Random(68710);
        for (int i = 0; i < 500; i++)
        {
            UInt128 value = ((UInt128)(ulong)random.NextInt64() << 65)
                | ((UInt128)(ulong)random.NextInt64() << 1) | (uint)random.Next(2);
            Check((BigInteger)value);
        }
    }

    [Fact]
    public void CompactRootIterationsAllocateNoScratchObjects()
    {
        UInt128[] values = [0, 1, 2, 3, (UInt128)1 << 64,
            ((UInt128)1 << 106) - 1, UInt128.MaxValue];
        UInt128 result = 0;
        for (int i = 0; i < 1000; i++)
            result |= ExactPopulationDeviation.CompactIntegerRoot(values[i % values.Length]);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 4000; i++)
            result |= ExactPopulationDeviation.CompactIntegerRoot(values[i % values.Length]);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(result != 0);
    }

    private static void Check(BigInteger value)
    {
        var root = ExactPopulationDeviation.IntegerRoot(value);
        Assert.True(root * root <= value);
        Assert.True((root + 1) * (root + 1) > value);
        if (value.GetBitLength() <= 128)
            Assert.Equal(root, (BigInteger)ExactPopulationDeviation.CompactIntegerRoot((UInt128)value));
    }
}

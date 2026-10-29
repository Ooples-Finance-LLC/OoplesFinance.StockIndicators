using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class MeanDiagnosticsWordCountTests
{
    [Fact]
    public void WordAndWideCountBoundariesMatchIndependentExactFractions()
    {
        var random = new Random(281031);
        foreach (int count in new[] { 1, 2, 3, 14, 20, 255, 1024, 2047, 2048, 2049, int.MaxValue })
        foreach (int width in new[] { 1, 31, 52, 53, 63, 64, 65, 84, 110, 128 })
        foreach (int scale in new[] { -900, -500, 0, 500 })
        {
            UInt128 mask = width == 128 ? UInt128.MaxValue : ((UInt128)1 << width) - 1;
            UInt128 numerator = (((UInt128)(ulong)random.NextInt64() << 64)
                | (ulong)random.NextInt64()) & mask;
            numerator |= (UInt128)1 << (width - 1);
            Assert.True(CertifiedMeanRelativeError.TryDivideCount(numerator, count, scale, out double actual));
            Assert.Equal(BitConverter.DoubleToInt64Bits(Reference(numerator, count, scale)),
                BitConverter.DoubleToInt64Bits(actual));
        }
    }

    [Fact]
    public void EvenAndOddDivisorsPreserveDiscardedMidpointBitsAndCarry()
    {
        foreach (int discarded in new[] { 1, 2, 20, 63 })
        foreach (int count in new[] { 1, 2, 3, 14, 20, 2047, 2048, 2049 })
        foreach (ulong significand in new[] { 1UL << 52, (1UL << 52) + 1, (1UL << 53) - 1 })
        foreach (int offset in new[] { -1, 0, 1 })
        {
            UInt128 midpoint = (((UInt128)significand << discarded) + ((UInt128)1 << (discarded - 1))) * (uint)count;
            UInt128 numerator = offset < 0 ? midpoint - 1 : midpoint + (uint)offset;
            Assert.True(CertifiedMeanRelativeError.TryDivideCount(numerator, count, -discarded, out double actual));
            Assert.Equal(BitConverter.DoubleToInt64Bits(Reference(numerator, count, -discarded)),
                BitConverter.DoubleToInt64Bits(actual));
        }
    }

    private static double Reference(UInt128 numerator, int count, int scale)
    {
        var exact = new ReferenceFraction((BigInteger)numerator) / new ReferenceFraction(count);
        var factor = new ReferenceFraction(BigInteger.One << Math.Abs(scale));
        return (scale < 0 ? exact / factor : exact * factor).ToDouble();
    }
}

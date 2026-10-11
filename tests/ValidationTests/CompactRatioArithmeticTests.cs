using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class CompactRatioArithmeticTests
{
    private static JurikDyadic Exact(UInt128 integer, int exponent, bool negative = false)
    {
        var value = new JurikDyadic();
        for (int shift = 0; shift < 128; shift += 32)
            value.Add(Math.ScaleB((uint)(integer >> shift), shift));
        value.ScaleByPowerOfTwo(exponent);
        if (negative) value.Multiply(-1);
        return value;
    }

    private static void Check(UInt128 numerator, UInt128 denominator, int exponent, bool negative)
    {
        var top = new ReferenceFraction((BigInteger)numerator);
        var bottom = new ReferenceFraction((BigInteger)denominator);
        var power = new ReferenceFraction(BigInteger.One << Math.Abs(exponent));
        var expected = exponent >= 0 ? top * power / bottom : top / (power * bottom);
        if (negative) expected = new ReferenceFraction(-1) * expected;
        var actual = Exact(numerator, exponent, negative).Ratio(Exact(denominator, 0));
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected.ToDouble()), BitConverter.DoubleToInt64Bits(actual));
    }

    [Fact]
    public void WideCompactRatiosMatchIndependentRationalsAcrossBinary64Range()
    {
        var random = new Random(293);
        for (int i = 0; i < 1200; i++)
        {
            UInt128 Number() => ((UInt128)(ulong)random.NextInt64() << 64) | (ulong)random.NextInt64() | 1;
            var top = Number() >> random.Next(0, 126);
            var bottom = (Number() >> random.Next(0, 126)) | 1;
            Check(top, bottom, random.Next(-1250, 1150), (i & 1) != 0);
        }
    }

    [Fact]
    public void RoundingTiesAdjacentFractionsAndExtremeShiftsRemainExact()
    {
        UInt128[] values = [1, 3, ((UInt128)1 << 53) - 1, ((UInt128)1 << 53) + 1,
            ((UInt128)1 << 54) + 3, ((UInt128)1 << 126) - 1, ((UInt128)1 << 127) - 1];
        foreach (var top in values)
        foreach (var bottom in values)
        foreach (int exponent in new[] { -1200, -1076, -1075, -1074, -1022, -53, 0, 971, 1023, 1100 })
        {
            Check(top, bottom, exponent, false);
            Check(top, bottom, exponent, true);
        }
        Assert.Equal(0d, Exact(1, 0).Ratio(default));
        Assert.Equal(0d, default(JurikDyadic).Ratio(Exact(1, 0)));
    }

    [Fact]
    public void WideCompactRatioPublicationAllocatesNoObjects()
    {
        var top = Exact(((UInt128)1 << 125) + 173, -75);
        var bottom = Exact(((UInt128)1 << 124) + 91, -74);
        double value = 0;
        for (int i = 0; i < 1000; i++) value = top.Ratio(bottom);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 4000; i++) value = top.Ratio(bottom);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(1d, value);
    }
}

using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class CompactRatioArithmeticTests
{
    [Fact]
    public void CompactMeansPreserveRoundingAndExtendedUnpublishedRange()
    {
        UInt128[] values = [0, 1, 3, ((UInt128)1 << 53) + 1, ((UInt128)1 << 100) + 17,
            ((UInt128)1 << 127) - 1];
        foreach (var integer in values)
        foreach (int exponent in new[] { -1200, -1075, -1074, -1022, 0, 971, 1023, 1100 })
        foreach (int count in new[] { 1, 2, 3, 10, 50, int.MaxValue })
        foreach (bool negative in new[] { false, true })
        {
            var value = Exact(integer, exponent, negative);
            BigInteger numerator = (BigInteger)integer * (negative ? -1 : 1), denominator = count;
            int shift = exponent + 1074;
            if (shift >= 0) numerator <<= shift; else denominator <<= -shift;
            Assert.Equal(RocBankValue.RoundUnits(numerator, denominator), value.Rounded(count).RoundedUnits());
            var expected = (new ReferenceFraction(numerator) / new ReferenceFraction(denominator << 1074)).ToDouble();
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(value.Mean(count)));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => default(JurikDyadic).Rounded(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => default(JurikDyadic).Mean(-1));
    }

    [Fact]
    public void CompactMeanPublicationAllocatesNoObjects()
    {
        var value = Exact(((UInt128)1 << 100) + 17, -80);
        double result = 0;
        for (int i = 0; i < 1000; i++) result = value.Rounded(10).Mean(1);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 4000; i++) result = value.Rounded(10).Mean(1);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(double.IsFinite(result));
    }

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

    [Fact]
    public void CertifiedDivisionMatchesIndependentRationalsAndExactMidpointTies()
    {
        var random = new Random(91473);
        int accepted = 0;
        for (int i = 0; i < 4000; i++)
        {
            UInt128 Number() => ((UInt128)(ulong)random.NextInt64() << 64) | (ulong)random.NextInt64() | 1;
            UInt128 top = (Number() >> random.Next(0, 127)) | 1;
            UInt128 bottom = (Number() >> random.Next(0, 127)) | 1;
            int scale = random.Next(-1100, 1100);
            if (!CertifiedCompactRatio.TryDivide(top, bottom, scale, out double actual)) continue;
            accepted++;
            var expected = new ReferenceFraction((BigInteger)top) / new ReferenceFraction((BigInteger)bottom);
            var factor = new ReferenceFraction(BigInteger.One << Math.Abs(scale));
            expected = scale >= 0 ? expected * factor : expected / factor;
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected.ToDouble()), BitConverter.DoubleToInt64Bits(actual));
        }
        Assert.True(accepted > 2000);
        UInt128 common = ((UInt128)1 << 70) + 3;
        foreach (int delta in new[] { -1, 0, 1, 2, 3 })
        {
            UInt128 top = (UInt128)((1L << 54) + delta) * common;
            UInt128 bottom = ((UInt128)1 << 54) * common;
            Assert.True(CertifiedCompactRatio.TryDivide(top, bottom, 0, out double actual));
            var expected = (new ReferenceFraction((BigInteger)top) / new ReferenceFraction((BigInteger)bottom)).ToDouble();
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
        }
        Assert.False(CertifiedCompactRatio.TryDivide(1, 1, -1074, out _));
        Assert.False(CertifiedCompactRatio.TryDivide(1, 1, 1024, out _));
        Assert.False(CertifiedCompactRatio.TryDivide(0, 1, 0, out _));
        Assert.False(CertifiedCompactRatio.TryDivide(1, 0, 0, out _));
    }
}

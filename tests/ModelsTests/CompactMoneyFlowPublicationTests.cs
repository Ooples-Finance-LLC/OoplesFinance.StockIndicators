using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class CompactMoneyFlowPublicationTests
{
    [Fact]
    public void CompactSumsMatchTheWideIntegerReference()
    {
        var random = new Random(73389);
        for (int width = 64; width <= 128; width++)
        for (int replay = 0; replay < 8; replay++)
        {
            var bytes = new byte[16]; random.NextBytes(bytes);
            var integer = (new BigInteger(bytes, isUnsigned: true) & ((BigInteger.One << (width - 1)) - 1))
                | (BigInteger.One << (width - 1)) | BigInteger.One;
            foreach (int scale in new[] { 0, 50, 1074, 2098 - width })
            foreach (int sign in new[] { -1, 1 }) Check(sign * integer, scale);
        }
    }

    [Fact]
    public void GuardStickyParityAndMantissaCarryPreserveBothSigns()
    {
        BigInteger leading = BigInteger.One << 64;
        foreach (BigInteger midpoint in new[] { leading + 2048, leading + 6144, (leading << 1) - 2048 })
        foreach (int offset in new[] { -1, 0, 1 })
        foreach (int sign in new[] { -1, 1 }) Check(sign * (midpoint + offset), 1074);
        var tie = FromInteger(leading + 2048, 1074);
        Equal(Math.ScaleB(1, 64), tie.SumWithCompactRounding());
        var oddTie = FromInteger(leading + 6144, 1074);
        Equal(Math.ScaleB(1, 64) + 8192, oddTie.SumWithCompactRounding());
        foreach (int sign in new[] { -1, 1 }) Check(sign * ((BigInteger.One << 128) - 1), 1970);
    }

    [Fact]
    public void SmallLargeFractionalAndExtendedSumsRetainTheirOriginalRounding()
    {
        foreach (int sign in new[] { -1, 1 })
        {
            Check(sign * ((BigInteger.One << 63) - 1), 0);
            Check(sign * ((BigInteger.One << 200) + 1), 0);
            var fractional = FromInteger(sign * ((BigInteger.One << 100) + 1), 0);
            fractional.ScaleByPowerOfTwo(-100);
            Equal(fractional.Mean(1), fractional.SumWithCompactRounding());
            var extended = new ExactMeanAccumulator();
            extended.Add(sign * double.MaxValue); extended.Add(sign * double.MaxValue);
            Equal(extended.Mean(1), extended.SumWithCompactRounding());
        }
        var zero = new ExactMeanAccumulator(); zero.Add(-0d);
        Equal(0, zero.SumWithCompactRounding());
    }

    [Fact]
    public void MoneyFlowPreviewResetAndExtendedCancellationRetainExactState()
    {
        var window = new MoneyFlowAccumulationWindow();
        Equal(double.MaxValue, window.Next(1, 0, 1, double.MaxValue, true).Publish());
        var preview = window.Next(1, 0, 1, double.MaxValue, false);
        Assert.True(preview.UpperShift > 0);
        Equal(double.PositiveInfinity, preview.Publish());
        Equal(0, window.Next(1, 0, 0, double.MaxValue, true).Publish());
        window.Next(1, 0, 1, double.MaxValue, true);
        var extended = window.Next(1, 0, 1, double.MaxValue, true);
        Assert.True(extended.UpperShift > 0);
        Equal(double.MaxValue, window.Next(1, 0, 0, double.MaxValue, true).Publish());
        window.Reset();
        Equal(0, window.Next(1, 1, 1, double.MaxValue, true).Publish());
    }

    private static void Check(BigInteger integer, int scale)
    {
        var sum = FromInteger(integer, scale);
        double expected = ExactMeanAccumulator.UnitRatio(integer << scale, BigInteger.One);
        Equal(expected, sum.Mean(1));
        Equal(expected, sum.SumWithCompactRounding());
    }

    private static ExactMeanAccumulator FromInteger(BigInteger integer, int scale)
    {
        var sum = new ExactMeanAccumulator();
        int sign = integer.Sign, offset = 0;
        for (var magnitude = BigInteger.Abs(integer); !magnitude.IsZero; magnitude >>= 32, offset += 32)
        {
            uint chunk = (uint)(magnitude & uint.MaxValue);
            if (chunk != 0) sum.Add(Math.ScaleB(sign * (double)chunk, scale + offset - 1074));
        }
        return sum;
    }

    private static void Equal(double expected, double actual) =>
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
}

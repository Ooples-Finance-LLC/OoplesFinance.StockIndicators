using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class ScaledRocUnitsTests
{
    [Fact]
    public void CompactWidthUnderflowAndExtendedOverflowMatchOriginalRounding()
    {
        BigInteger[] factors = [0, 1, 3, (BigInteger.One << 53) - 1,
            (BigInteger.One << 107) + 1, (BigInteger.One << 127) - 1,
            (BigInteger.One << 128) - 1, (BigInteger.One << 129) + 1];
        foreach (var numerator in factors)
        foreach (var denominator in factors.Where(v => v > 0))
        foreach (int scale in new[] { 0, 1, 52, 1022, 1074, 2044, 2200 })
        {
            Check(numerator, denominator, scale);
            Check(-numerator, denominator, scale);
        }
    }

    [Fact]
    public void MidpointsPreserveNearestEvenAndCarry()
    {
        foreach (long mantissa in new[] { 1L << 52, (1L << 52) + 1, (1L << 53) - 1 })
        foreach (int scale in new[] { 0, 1, 52, 1022, 2043, 2044, 2076 })
        foreach (int offset in new[] { -1, 0, 1 })
        {
            BigInteger numerator = 2 * (BigInteger)(2 * mantissa + 1) + offset;
            Check(numerator, 4, scale);
            Check(-numerator, 4, scale);
        }
    }

    [Fact]
    public void MixedScaledRatiosMatchIndependentReference()
    {
        var random = new Random(301028);
        for (int i = 0; i < 2000; i++)
        {
            BigInteger numerator = new BigInteger(random.NextInt64()) << random.Next(0, 140);
            numerator += i % 3 - 1;
            if (i % 2 == 0) numerator = -numerator;
            BigInteger denominator = new BigInteger(random.NextInt64(1, long.MaxValue)) << random.Next(0, 200);
            Check(numerator, denominator, random.Next(0, 2200));
        }
    }

    [Fact]
    public void InvalidInputsAreRejectedBeforeZeroShortcut()
    {
        foreach (int numerator in new[] { 0, 1, -1 })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RocBankValue.RoundScaledUnits(numerator, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => RocBankValue.RoundScaledUnits(numerator, -1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => RocBankValue.RoundScaledUnits(numerator, 1, -1));
        }
    }

    private static void Check(BigInteger numerator, BigInteger denominator, int scale)
    {
        BigInteger units = numerator << scale;
        BigInteger expected;
        for (int overflowShift = 0; ; overflowShift += 32)
        {
            double rounded = ExactMeanAccumulator.UnitRatio(units, denominator << overflowShift);
            if (double.IsInfinity(rounded)) continue;
            expected = ExactVarianceWindow.Units(rounded) << overflowShift;
            break;
        }
        Assert.Equal(expected, RocBankValue.RoundScaledUnits(numerator, denominator, scale));
    }
}

using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class CompactRocUnitsTests
{
    [Fact]
    public void CompactAndFallbackRatiosPreserveOriginalUnitRounding()
    {
        BigInteger[] factors = [1, 3, 7, (BigInteger.One << 53) - 1,
            (BigInteger.One << 53) + 1, (BigInteger.One << 127) - 1,
            (BigInteger.One << 128) - 1, (BigInteger.One << 129) + 1];
        foreach (var numerator in factors)
        foreach (var denominator in factors)
        foreach (int shift in new[] { 0, 1, 52, 1073, 1074, 1500, 2097, 2130 })
        {
            Check(numerator << shift, denominator);
            Check(-(numerator << shift), denominator);
            Check(numerator << shift, denominator << 700);
        }
        Check(0, 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => RocBankValue.RoundUnits(1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => RocBankValue.RoundUnits(1, -1));
    }

    [Fact]
    public void NormalHalfwayRatiosRetainTiesToEven()
    {
        foreach (int exponent in new[] { -1022, -100, 0, 500, 1023 })
        foreach (long mantissa in new[] { (1L << 52), (1L << 52) + 1, (1L << 53) - 1 })
        {
            BigInteger midpoint = (BigInteger)(2 * mantissa + 1) << (exponent + 1022);
            Check(midpoint, 2);
            Check(-midpoint, 2);
            Check((midpoint << 1) - 1, 4);
            Check((midpoint << 1) + 1, 4);
        }
    }

    [Fact]
    public void IntegerUnitsPreserveGuardStickyCarryAndOverflowBoundaries()
    {
        foreach (int shift in new[] { 1, 2, 52, 700, 1022, 2043, 2044, 2045, 2077 })
        foreach (long mantissa in new[] { 1L << 52, (1L << 52) + 1, (1L << 53) - 2, (1L << 53) - 1 })
        {
            BigInteger exact = (BigInteger)mantissa << shift;
            BigInteger half = BigInteger.One << (shift - 1);
            foreach (var value in new[] { exact, exact + half - 1, exact + half, exact + half + 1 })
            {
                Check(value, 1);
                Check(-value, 1);
            }
        }
        foreach (var value in new BigInteger[] { 0, 1, (BigInteger.One << 52) - 1,
                     BigInteger.One << 52, (BigInteger.One << 53) - 1 })
        {
            Check(value, 1);
            Check(-value, 1);
        }
    }

    [Fact]
    public void BinaryDivisorsPreserveUnderflowTiesCarryAndFallbackBoundaries()
    {
        foreach (int divisorShift in new[] { 1, 2, 55, 56, 1074, 4096, 4097 })
        foreach (int grid in new[] { 0, 1, 52, 1022, 2044, 2045 })
        foreach (long mantissa in new[] { 0L, 1L, 3L, 1L << 52, (1L << 52) + 1, (1L << 53) - 1 })
        {
            BigInteger divisor = BigInteger.One << divisorShift;
            int discard = divisorShift + grid;
            BigInteger exact = (BigInteger)mantissa << discard;
            BigInteger half = BigInteger.One << (discard - 1);
            foreach (var value in new[] { exact, exact + half - 1, exact + half, exact + half + 1 })
            {
                Check(value, divisor);
                Check(-value, divisor);
            }
        }
    }

    private static void Check(BigInteger numerator, BigInteger denominator)
    {
        BigInteger expected;
        for (int shift = 0; ; shift += 32)
        {
            double value = ExactMeanAccumulator.UnitRatio(numerator, denominator << shift);
            if (double.IsInfinity(value)) continue;
            expected = ExactVarianceWindow.Units(value) << shift;
            break;
        }
        Assert.Equal(expected, RocBankValue.RoundUnits(numerator, denominator));
    }
}

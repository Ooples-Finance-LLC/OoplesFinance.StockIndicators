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

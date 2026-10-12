using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class MoneyFlowPercentageTests
{
    [Fact]
    public void SignedLimitsBinadesAndRandomRatiosMatchIndependentIntegers()
    {
        long[] values = [long.MinValue, long.MinValue + 1, -(1L << 53) - 1,
            -(1L << 53), -3, -1, 0, 1, 3, (1L << 53) - 1, 1L << 53,
            (1L << 53) + 1, long.MaxValue - 1, long.MaxValue];
        foreach (long positive in values)
        foreach (long total in values.Where(v => v != 0)) Check(positive, total);
        for (int bit = 1; bit < 63; bit++)
        foreach (long offset in new long[] { -1, 0, 1 })
        {
            Check((1L << bit) + offset, 1);
            Check(1, (1L << bit) + offset);
            Check((1L << bit) + offset, (1L << bit) - 1);
        }
        var random = new Random(50183);
        var bytes = new byte[16];
        for (int i = 0; i < 4000; i++)
        {
            random.NextBytes(bytes);
            Check(BitConverter.ToInt64(bytes, 0), BitConverter.ToInt64(bytes, 8) | 1);
        }
    }

    [Fact]
    public void HalfwayRoundingSelectsEvenMantissaAndPreservesSign()
    {
        // The 60-bit numerator discards seven bits. Offsets 16 and 48
        // leave remainder 64, with even and odd retained quotients respectively.
        foreach (long numerator in new[] { (1L << 53) + 16, (1L << 53) + 48 })
        foreach (long sign in new long[] { -1, 1 })
        foreach (long denominator in new long[] { -256, 256 }) Check(sign * numerator, denominator);
    }

    private static void Check(long positive, long total)
    {
        BigInteger denominator = total;
        double expected = ExactMeanAccumulator.UnitRatio(
            ((BigInteger)positive * 100 * denominator.Sign) << 1074, BigInteger.Abs(denominator));
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected),
            BitConverter.DoubleToInt64Bits(MoneyFlowPercentage.Calculate(positive, total)));
    }
}

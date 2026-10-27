using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class CompactMacdValueTests
{
    [Fact]
    public void ExactInputsAndWeightedRoundsMatchWideUnits()
    {
        double[] values = [0, -0d, double.Epsilon, -double.Epsilon,
            Math.BitDecrement(Math.ScaleB(1, -1022)), Math.ScaleB(1, -1022),
            .15, .075, 1, -1, Math.BitIncrement(1), double.MaxValue, -double.MaxValue];
        foreach (double a in values)
        foreach (double b in values)
        foreach (int period in new[] { 1, 3, 26, int.MaxValue })
        {
            var left = CompactMacdValue.Of(a); var right = CompactMacdValue.Of(b);
            Assert.Equal(ExactVarianceWindow.Units(a), left.Units);
            Assert.Equal(ExactVarianceWindow.Units(b), right.Units);
            var actual = (left.Times(period - 1) + right.Times(2)).Round((long)period + 1);
            var expected = RocBankValue.RoundUnits(left.Units * (period - 1) + right.Units * 2, (long)period + 1);
            Assert.Equal(expected, actual.Units);
            Assert.Equal(BitConverter.DoubleToInt64Bits(ExactMeanAccumulator.UnitRatio(expected, 1)),
                BitConverter.DoubleToInt64Bits(actual.Publish()));
        }
    }
    [Fact]
    public void FixedComplementsAndPercentagePreserveCancellationAndExtendedRange()
    {
        foreach (double coefficient in new[] { .15, .075 })
        {
            var factor = new ExactMeanAccumulator.ProductFactor(coefficient);
            BigInteger denominator = BigInteger.One << (1074 - factor.Scale);
            foreach (int exponent in new[] { 0, 1, 51, 52, 1074, 2097, 2098, 2200 })
            foreach (int sign in new[] { -1, 1 })
            {
                BigInteger a = sign * ((BigInteger.One << exponent) + 1);
                BigInteger b = -(a >> 1);
                var left = CompactMacdValue.FromUnits(a); var right = CompactMacdValue.FromUnits(b);
                Assert.Equal(a, left.Units);
                var actual = (left.Times(denominator - factor.Mantissa) + right.Times(factor.Mantissa)).Round(denominator);
                Assert.Equal(RocBankValue.RoundUnits(a * (denominator - factor.Mantissa) + b * factor.Mantissa, denominator), actual.Units);
                if (!b.IsZero)
                    Assert.Equal(RocBankValue.RoundUnits((100 * (a - b) * b.Sign) << 1074, BigInteger.Abs(b)),
                        (left - right).Percentage(right).Units);
            }
        }
    }
    [Fact]
    public void MidpointsAndRandomRatiosRetainNearestEvenRounding()
    {
        var random = new Random(271027);
        for (int i = 0; i < 2000; i++)
        {
            BigInteger numerator = new BigInteger(random.NextInt64()) << random.Next(0, 2200);
            numerator += i % 3 - 1;
            if (i % 2 == 0) numerator = -numerator;
            BigInteger denominator = i % 3 == 0 ? 2 : random.NextInt64(1, long.MaxValue);
            Assert.Equal(RocBankValue.RoundUnits(numerator, denominator),
                CompactMacdValue.FromUnits(numerator).Round(denominator).Units);
        }
    }
    [Fact]
    public void SignedWordOverflowAndAlignmentUseExactFallback()
    {
        foreach (int width in new[] { 53, 64, 125, 126, 127, 128, 129, 512 })
        foreach (int sign in new[] { -1, 1 })
        foreach (int offset in new[] { -1, 0, 1 })
        {
            BigInteger units = sign * ((BigInteger.One << width) + offset);
            var value = CompactMacdValue.FromUnits(units);
            foreach (int weight in new[] { -3, -1, 0, 1, 2, 3, int.MaxValue })
                Assert.Equal(units * weight, value.Times(weight).Units);
            foreach (int shift in new[] { 0, 1, 64, 127, 128, 1074 })
            {
                var other = CompactMacdValue.FromUnits(BigInteger.One << shift);
                Assert.Equal(units + (BigInteger.One << shift), (value + other).Units);
                Assert.Equal(units - (BigInteger.One << shift), (value - other).Units);
            }
        }
        BigInteger half = BigInteger.One << 126;
        var minimum = CompactMacdValue.FromUnits(-half + 1) + CompactMacdValue.FromUnits(-half - 1);
        Assert.Equal(-(BigInteger.One << 127), minimum.Units);
        Assert.Equal(BigInteger.One << 127, minimum.Times(-1).Units);
        Assert.Equal(BigInteger.One << 127, (default(CompactMacdValue) - minimum).Units);
        Assert.Equal(RocBankValue.RoundUnits(minimum.Units, 3), minimum.Round(3).Units);
    }

}

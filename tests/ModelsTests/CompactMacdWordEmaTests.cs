using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class CompactMacdWordEmaTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(9)]
    [InlineData(19)]
    [InlineData(26)]
    [InlineData(2047)]
    public void SameBinadeRecurrencesMatchIndependentRationalRounding(int period)
    {
        var random = new Random(301031 + period);
        for (int i = 0; i < 1024; i++)
        {
            int exponent = random.Next(1, 2047);
            long leftBits = ((long)exponent << 52) | random.NextInt64(1L << 52);
            long rightBits = ((long)exponent << 52) | random.NextInt64(1L << 52);
            double left = BitConverter.Int64BitsToDouble(leftBits);
            double right = BitConverter.Int64BitsToDouble(rightBits);
            double expected = ((ReferenceFraction.FromDouble(left) * new ReferenceFraction(period - 1)
                + ReferenceFraction.FromDouble(right) * new ReferenceFraction(2))
                / new ReferenceFraction((long)period + 1)).ToDouble();
            var actual = CompactMacdValue.Of(left).AdvanceEma(CompactMacdValue.Of(right), period);
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual.Publish()));
            Assert.Equal(ExactVarianceWindow.Units(expected), actual.Units);
        }
    }

    [Theory]
    [InlineData(1L, 2L)]
    [InlineData(3L, 4L)]
    [InlineData(5404319552844595L, 1L << 55)]
    [InlineData(5404319552844595L, 1L << 56)]
    [InlineData((1L << 62) - 1, 1L << 62)]
    [InlineData(3L, 20L)]
    [InlineData(5L, 13L)]
    public void FixedWeightsMatchIndependentRationalRounding(long alpha, long denominator)
    {
        var random = new Random(301032);
        for (int i = 0; i < 1024; i++)
        {
            int exponent = random.Next(1, 2047);
            double prior = BitConverter.Int64BitsToDouble(((long)exponent << 52) | random.NextInt64(1L << 52));
            double input = BitConverter.Int64BitsToDouble(((long)exponent << 52) | random.NextInt64(1L << 52));
            double expected = ((ReferenceFraction.FromDouble(prior) * new ReferenceFraction(denominator - alpha)
                + ReferenceFraction.FromDouble(input) * new ReferenceFraction(alpha))
                / new ReferenceFraction(denominator)).ToDouble();
            var actual = CompactMacdValue.Of(prior).AdvanceFixedEma(CompactMacdValue.Of(input), alpha, denominator);
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual.Publish()));
            Assert.Equal(ExactVarianceWindow.Units(expected), actual.Units);
        }
    }

    [Fact]
    public void MidpointTiesAndBinadeEndpointsRoundToEven()
    {
        const long mask = (1L << 52) - 1;
        foreach (int exponent in new[] { 1, 2, 511, 1023, 1535, 2045, 2046 })
        foreach (long fraction in new[] { 0L, 1L, 2L, mask - 2, mask - 1 })
        foreach (bool reverse in new[] { false, true })
        {
            long low = ((long)exponent << 52) | fraction;
            double left = BitConverter.Int64BitsToDouble(reverse ? low + 1 : low);
            double right = BitConverter.Int64BitsToDouble(reverse ? low : low + 1);
            var actual = CompactMacdValue.Of(left).AdvanceEma(CompactMacdValue.Of(right), 3);
            long expectedBits = (low & 1) == 0 ? low : low + 1;
            Assert.Equal(expectedBits, BitConverter.DoubleToInt64Bits(actual.Publish()));
            var fixedActual = CompactMacdValue.Of(left).AdvanceFixedEma(CompactMacdValue.Of(right), 1, 2);
            Assert.Equal(expectedBits, BitConverter.DoubleToInt64Bits(fixedActual.Publish()));
        }
    }

    [Fact]
    public void UnsupportedDomainsRetainExactWideRecurrence()
    {
        var inputs = new[] { BigInteger.Zero, BigInteger.One, -BigInteger.One,
            ExactVarianceWindow.Units(double.MaxValue), ExactVarianceWindow.Units(-double.MaxValue),
            ExactVarianceWindow.Units(100), ExactVarianceWindow.Units(-100),
            BigInteger.One << 2400, -(BigInteger.One << 2400), (BigInteger.One << 127) - 1 };
        foreach (var prior in inputs)
        foreach (var input in inputs)
        foreach (int period in new[] { 1, 3, 2047, 2048, int.MaxValue })
        {
            BigInteger expected = RocBankValue.RoundUnits(prior * (period - 1) + input * 2, (long)period + 1);
            var actual = CompactMacdValue.FromUnits(prior).AdvanceEma(CompactMacdValue.FromUnits(input), period);
            Assert.Equal(expected, actual.Units);
        }
        foreach (var prior in inputs)
        foreach (var input in inputs)
        foreach (var weights in new (long Alpha, long Denominator)[] { (1L, 2L), (3L, 20L), (5404319552844595L, 1L << 55) })
        {
            BigInteger expected = RocBankValue.RoundUnits(prior * (weights.Denominator - weights.Alpha)
                + input * weights.Alpha, weights.Denominator);
            var actual = CompactMacdValue.FromUnits(prior).AdvanceFixedEma(
                CompactMacdValue.FromUnits(input), weights.Alpha, weights.Denominator);
            Assert.Equal(expected, actual.Units);
        }
    }
}

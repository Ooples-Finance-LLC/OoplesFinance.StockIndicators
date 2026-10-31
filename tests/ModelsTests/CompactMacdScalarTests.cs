using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class CompactMacdScalarTests
{
    [Fact]
    public void SignedWordProductsPreserveBothWordsAndBinaryScale()
    {
        long[] boundaries = [long.MinValue, long.MinValue + 1, -(1L << 53) - 1,
            -1, 0, 1, (1L << 53) + 1, long.MaxValue - 1, long.MaxValue];
        var random = new Random(719231);
        foreach (int scale in new[] { 0, 970, 1074, 2200 })
        {
            var unit = CompactMacdValue.FromUnits(BigInteger.One << scale);
            foreach (long coefficient in boundaries)
            foreach (long weight in boundaries)
                Check(coefficient, weight);
            for (int i = 0; i < 256; i++)
                Check(random.NextInt64(long.MinValue, long.MaxValue),
                    random.NextInt64(long.MinValue, long.MaxValue));

            void Check(long coefficient, long weight)
            {
                // The first product deliberately retains an unnormalized word,
                // including long.MinValue, for the second multiplication.
                var actual = unit.Times(coefficient).Times(weight);
                var expected = (new BigInteger(coefficient) * weight) << scale;
                Assert.Equal(expected, actual.Units);
                // Also exercise products that promote beyond signed 128 bits.
                Assert.Equal(expected * long.MaxValue, actual.Times(long.MaxValue).Units);
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    public void ScalarMultiplicationPreservesExactUnits(long weight)
    {
        foreach (var units in Values())
        {
            var value = CompactMacdValue.FromUnits(units);
            Assert.Equal(units * weight, value.Times(weight).Units);
            Assert.Equal(value.Times(new BigInteger(weight)).Units, value.Times(weight).Units);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(17)]
    [InlineData(2047)]
    [InlineData(2048)]
    [InlineData(2049)]
    [InlineData(int.MaxValue)]
    [InlineData((long)int.MaxValue + 1)]
    [InlineData(long.MaxValue)]
    public void ScalarRoundingMatchesWideRoundingIncludingExtendedValues(long divisor)
    {
        foreach (var units in Values())
        {
            var value = CompactMacdValue.FromUnits(units);
            var rounded = value.Round(divisor);
            Assert.Equal(RocBankValue.RoundUnits(units, divisor), rounded.Units);
            Assert.Equal(value.Round(new BigInteger(divisor)).Units, rounded.Units);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void InvalidScalarDivisorRemainsRejected(long divisor)
    {
        foreach (var units in new[] { BigInteger.Zero, BigInteger.One, BigInteger.One << 2200 })
            Assert.Throws<ArgumentOutOfRangeException>(() => CompactMacdValue.FromUnits(units).Round(divisor));
    }

    private static IEnumerable<BigInteger> Values()
    {
        yield return BigInteger.Zero;
        foreach (int width in new[] { 1, 52, 53, 54, 64, 106, 127, 128, 129 })
        foreach (int scale in new[] { 0, 1, 52, 970, 1074, 2045, 2200 })
        foreach (int offset in new[] { -1, 0, 1 })
        foreach (int sign in new[] { -1, 1 })
            yield return sign * (((BigInteger.One << width) + offset) << scale);
    }
}

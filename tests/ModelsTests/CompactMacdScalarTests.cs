using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class CompactMacdScalarTests
{
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
    [InlineData(int.MaxValue)]
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

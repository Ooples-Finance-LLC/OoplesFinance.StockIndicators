using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class CompactMacdBitPublicationTests
{
    [Fact]
    public void EveryFiniteExponentRetainsInputBits()
    {
        const ulong mask = (1UL << 52) - 1;
        foreach (int exponent in Enumerable.Range(0, 2047))
        foreach (ulong fraction in new[] { 0UL, 1UL, mask >> 1, mask })
        foreach (ulong sign in new[] { 0UL, 1UL << 63 })
        {
            ulong bits = sign | ((ulong)exponent << 52) | fraction;
            double value = BitConverter.Int64BitsToDouble(unchecked((long)bits));
            // Exact integer state intentionally canonicalizes both input zeros.
            long expected = value == 0 ? 0 : unchecked((long)bits);
            Assert.Equal(expected,
                BitConverter.DoubleToInt64Bits(CompactMacdValue.Of(value).Publish()));
        }
    }

    [Fact]
    public void ReducedSignificandsAndFallbackBoundariesMatchWidePublisher()
    {
        foreach (int width in new[] { 1, 2, 31, 32, 51, 52, 53, 54, 63, 64, 106, 127, 128, 129 })
        foreach (int scale in new[] { 0, 1, 51, 52, 53, 970, 1022, 1074, 2044, 2045, 2046, 2097, 2200 })
        foreach (int sign in new[] { -1, 1 })
        foreach (int offset in new[] { -1, 0, 1 })
        {
            BigInteger units = sign * (((BigInteger.One << (width - 1)) + offset) << scale);
            double expected = ExactMeanAccumulator.UnitRatio(units, 1);
            double actual = CompactMacdValue.FromUnits(units).Publish();
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
        }
    }

    [Fact]
    public void CancellationAndUnnormalizedCompactSumsPublishExactBits()
    {
        var random = new Random(301030);
        foreach (int exponent in new[] { -1074, -1022, -1021, -511, 0, 511, 1022 })
        for (int i = 0; i < 127; i++)
        {
            double a = Math.ScaleB(random.NextDouble() * 2 - 1, exponent);
            double b = Math.ScaleB(random.NextDouble() * 2 - 1, exponent);
            var left = CompactMacdValue.Of(a);
            var right = CompactMacdValue.Of(b);
            foreach (var actual in new[] { left + right, left - right, left - left })
            {
                double expected = ExactMeanAccumulator.UnitRatio(actual.Units, 1);
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected),
                    BitConverter.DoubleToInt64Bits(actual.Publish()));
            }
        }
    }
}

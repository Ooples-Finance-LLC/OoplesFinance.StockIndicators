using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class CompactMacdFixedRecurrenceTests
{
    // Exact binary64 representations, not the decimal fractions 3/20 and 3/40.
    private const long Alpha = 5404319552844595;

    [Fact]
    public void FixedRecurrencesMatchWideRoundingAtCancellationAndExponentBoundaries()
    {
        BigInteger[] values = [0, 1, -1, (BigInteger.One << 52) - 1,
            BigInteger.One << 1074, (BigInteger.One << 1127) - 1,
            -(BigInteger.One << 1127) + 1, BigInteger.One << 2200,
            -(BigInteger.One << 2200), (BigInteger.One << 127) - 1,
            -(BigInteger.One << 127)];
        foreach (long denominator in new[] { 1L << 55, 1L << 56 })
        foreach (var previous in values)
        foreach (var input in values)
        {
            var expected = RocBankValue.RoundUnits(previous * (denominator - Alpha) + input * Alpha, denominator);
            var actual = CompactMacdValue.FromUnits(previous)
                .AdvanceFixedEma(CompactMacdValue.FromUnits(input), Alpha, denominator);
            Assert.Equal(expected, actual.Units);
        }
    }

    [Fact]
    public void RepeatedFixedRecurrencesPreserveNormalSubnormalAndExtendedState()
    {
        foreach (long denominator in new[] { 1L << 55, 1L << 56 })
        foreach (int exponent in new[] { -1074, -1022, -450, 0, 450, 1020 })
        {
            var actual = default(CompactMacdValue);
            var expected = BigInteger.Zero;
            for (int i = 0; i < 257; i++)
            {
                double price = Math.ScaleB((i % 2 == 0 ? 1 : -1) * (1.25 + Math.Sin(i) * .2), exponent);
                var input = CompactMacdValue.Of(price);
                expected = RocBankValue.RoundUnits(expected * (denominator - Alpha) + input.Units * Alpha, denominator);
                actual = actual.AdvanceFixedEma(input, Alpha, denominator);
                Assert.Equal(expected, actual.Units);
            }
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => default(CompactMacdValue).AdvanceFixedEma(default, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => default(CompactMacdValue).AdvanceFixedEma(default, 1, 1));
    }
}

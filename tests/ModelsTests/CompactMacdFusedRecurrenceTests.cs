using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class CompactMacdFusedRecurrenceTests
{
    [Fact]
    public void FusedPeriodRecurrenceMatchesWideRoundingAcrossExponentAndCancellationBoundaries()
    {
        BigInteger[] values = [0, 1, -1, (BigInteger.One << 52) - 1,
            BigInteger.One << 1074, (BigInteger.One << 1127) - 1,
            -(BigInteger.One << 1127) + 1, BigInteger.One << 2200,
            -(BigInteger.One << 2200), (BigInteger.One << 127) - 1,
            -(BigInteger.One << 127)];
        foreach (var left in values)
        foreach (var right in values)
        foreach (int period in new[] { 1, 2, 9, 12, 26, int.MaxValue })
        {
            var previous = CompactMacdValue.FromUnits(left);
            var input = CompactMacdValue.FromUnits(right);
            var expected = RocBankValue.RoundUnits(left * (period - 1) + 2 * right, (long)period + 1);
            Assert.Equal(expected, previous.AdvanceEma(input, period).Units);
        }
    }

    [Fact]
    public void RepeatedRoundedRecurrencesPreserveStateForNormalAndSubnormalPrices()
    {
        foreach (int exponent in new[] { -1074, -1022, -450, 0, 450, 1020 })
        foreach (int period in new[] { 1, 9, 26, int.MaxValue })
        {
            var state = default(CompactMacdValue);
            var expected = BigInteger.Zero;
            for (int i = 0; i < 257; i++)
            {
                double price = Math.ScaleB((i % 2 == 0 ? 1 : -1) * (1.25 + Math.Sin(i) * .2), exponent);
                var input = CompactMacdValue.Of(price);
                expected = RocBankValue.RoundUnits(expected * (period - 1) + input.Units * 2, (long)period + 1);
                state = state.AdvanceEma(input, period);
                Assert.Equal(expected, state.Units);
            }
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => default(CompactMacdValue).AdvanceEma(default, 0));
    }
}

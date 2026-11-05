using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class VolatilityStopFiniteStageTests
{
    private static BigInteger Expected(double average, double multiplier, double extreme, bool bullish)
    {
        var width = RocBankValue.RoundUnits(ExactVarianceWindow.Units(average)
            * ExactVarianceWindow.Units(multiplier), BigInteger.One << 1074);
        var price = ExactVarianceWindow.Units(extreme);
        return RocBankValue.RoundUnits(bullish ? price - width : price + width, 1);
    }

    [Fact]
    public void FiniteStagesMatchIndependentFullGridWidthAndStopRounding()
    {
        int accepted = 0;
        foreach (double average in new[] { 0, double.Epsilon, 1e-300, .1, 1, 1e100, double.MaxValue / 4 })
        foreach (double multiplier in new[] { .1, .75, 3, Math.BitIncrement(1d), 1e100 })
        foreach (double extreme in new[] { -double.MaxValue, -1e100, -1, 0, 1, 1e100, double.MaxValue })
        foreach (bool bullish in new[] { false, true })
        {
            if (!VolatilityStopSnapshot.TryFiniteStop(new(average), multiplier, extreme, bullish, out var actual)) continue;
            Assert.Equal(Expected(average, multiplier, extreme, bullish), actual);
            accepted++;
        }
        Assert.True(accepted > 200);
    }

    [Fact]
    public void SubnormalWidthsExtendedAtrAndOverflowKeepOriginalPath()
    {
        Assert.False(VolatilityStopSnapshot.TryFiniteStop(new(double.Epsilon), 1, 1, true, out _));
        Assert.False(VolatilityStopSnapshot.TryFiniteStop(new(1, 1), 3, 1, true, out _));
        Assert.False(VolatilityStopSnapshot.TryFiniteStop(new(1), double.MaxValue, -double.MaxValue, true, out _));
        Assert.False(VolatilityStopSnapshot.TryFiniteStop(new(double.MaxValue), 2, 0, true, out _));
    }

    [Fact]
    public void WidthRoundsBeforeCancellationInsteadOfFusingStages()
    {
        Assert.True(VolatilityStopSnapshot.TryFiniteStop(new(.1), 3, .3, true, out var actual));
        Assert.Equal(Expected(.1, 3, .3, true), actual);
        Assert.NotEqual(ExactVarianceWindow.Units(Math.FusedMultiplyAdd(-.1, 3, .3)), actual);
    }

    [Fact]
    public void ZeroWidthRetainsCanonicalExactZeroStopUnits()
    {
        foreach (double extreme in new[] { 0d, BitConverter.Int64BitsToDouble(long.MinValue) })
        foreach (bool bullish in new[] { false, true })
        {
            Assert.True(VolatilityStopSnapshot.TryFiniteStop(new(0), 3, extreme, bullish, out var actual));
            Assert.Equal(BigInteger.Zero, actual);
        }
    }
}

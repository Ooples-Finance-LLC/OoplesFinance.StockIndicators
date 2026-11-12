using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class HilbertWordBlendTests
{
    private static bool Check(double value, double old)
    {
        if (!HilbertPhaseState.TryWordBlend(value, old, out double actual)) return false;
        // Independent unfactored products on the full binary64 integer grid.
        BigInteger numerator = ExactVarianceWindow.Units(value) * ExactVarianceWindow.Units(.2)
            + ExactVarianceWindow.Units(old) * ExactVarianceWindow.Units(.8);
        double expected = ExactMeanAccumulator.UnitRatio(numerator, BigInteger.One << 1074);
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected == 0 ? 0 : expected),
            BitConverter.DoubleToInt64Bits(actual));
        return true;
    }

    [Fact]
    public void ExactProductsMatchIndependentReferenceAcrossSignsAndExponents()
    {
        var random = new Random(261112);
        int accepted = 0, rejected = 0;
        for (int i = 0; i < 12000; i++)
        {
            int exponent = random.Next(-1022, 1024);
            int otherExponent = i % 2 == 0 ? Math.Clamp(exponent + random.Next(-24, 25), -1022, 1023)
                : random.Next(-1022, 1024);
            double a = Math.ScaleB((.5 + random.NextDouble() * .5) * (i % 3 == 0 ? -1 : 1), exponent);
            double b = Math.ScaleB((.5 + random.NextDouble() * .5) * (i % 5 == 0 ? -1 : 1), otherExponent);
            if (Check(a, b)) accepted++; else rejected++;
        }
        Assert.True(accepted > 5000, $"Only {accepted} accepted cases.");
        Assert.True(rejected > 5000, $"Only {rejected} fallback cases.");
    }

    [Fact]
    public void CancellationZeroAndRoundingBoundariesMatchIndependentReference()
    {
        foreach (int exponent in new[] { -900, -500, -1, 0, 500, 970 })
        foreach (double mantissa in new[] { 1d, Math.BitIncrement(1d), Math.BitDecrement(2d) })
        {
            double a = Math.ScaleB(mantissa, exponent), b = -a / 4;
            Assert.True(Check(a, b));
            Assert.True(Check(a, Math.BitIncrement(b)));
            Assert.True(Check(a, Math.BitDecrement(b)));
            Assert.True(Check(-a, -Math.BitIncrement(b)));
            Assert.True(Check(a, 0));
            Assert.True(Check(0, a));
        }
        Assert.True(Check(-0d, 0));
        Assert.True(Check(double.MaxValue, -double.MaxValue));
        Assert.True(Check(double.MaxValue, double.MaxValue));
    }

    [Fact]
    public void UnsupportedAlignmentAndNonNormalResultsRetainFallback()
    {
        Assert.False(HilbertPhaseState.TryWordBlend(1, double.Epsilon, out _));
        Assert.False(HilbertPhaseState.TryWordBlend(double.Epsilon, 0, out _));
        Assert.False(HilbertPhaseState.TryWordBlend(Math.ScaleB(1, -1022), 0, out _));
        foreach (double value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.False(HilbertPhaseState.TryWordBlend(value, 1, out _));
            Assert.False(HilbertPhaseState.TryWordBlend(1, value, out _));
        }
    }
}

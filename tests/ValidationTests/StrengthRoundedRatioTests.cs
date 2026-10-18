using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class StrengthRoundedRatioTests
{
    [Fact]
    public void CertifiedRatiosMatchExactRationalIncludingSignedCancellation()
    {
        var random = new Random(84171);
        int accepted = 0;
        for (int i = 0; i < 10000; i++)
        {
            long gain = random.NextInt64(1, 1L << 56), loss = random.NextInt64(1, 1L << 56);
            foreach (bool signed in new[] { false, true })
            {
                long numerator = (signed ? gain - loss : gain) * 100;
                long denominator = gain + loss;
                if (!StrengthRoundedRatio.TryDivide(numerator, denominator, out double actual)) continue;
                accepted++;
                double expected = (new ReferenceFraction(numerator) / new ReferenceFraction(denominator)).ToDouble();
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
            }
        }
        Assert.True(accepted > 18000, $"Only {accepted} of 20000 ordinary ratios certified.");
    }

    [Fact]
    public void IntegerConversionEdgesAndRoundingCellsNeverAcceptWrongResults()
    {
        foreach (long numerator in new[] { long.MinValue, -100L * (1L << 56), -(1L << 53) - 1,
            -1L, 0L, 1L, (1L << 53) - 1, (1L << 53) + 1, 100L * (1L << 56) - 1, long.MaxValue })
        foreach (long denominator in new[] { 1L, 2L, 3L, (1L << 53) - 1, (1L << 53) + 1, (1L << 57) - 1 })
        {
            if (!StrengthRoundedRatio.TryDivide(numerator, denominator, out double actual)) continue;
            double expected = (new ReferenceFraction(numerator) / new ReferenceFraction(denominator)).ToDouble();
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
        }
        Assert.False(StrengthRoundedRatio.TryDivide(1, 0, out _));
        Assert.False(StrengthRoundedRatio.TryDivide(1, -1, out _));
        Assert.False(StrengthRoundedRatio.TryDivide(1, 1L << 57, out _));
    }
}

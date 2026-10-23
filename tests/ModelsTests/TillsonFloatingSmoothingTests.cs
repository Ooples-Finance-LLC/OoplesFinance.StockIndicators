using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class TillsonFloatingSmoothingTests
{
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(20)]
    [InlineData(1024)] [InlineData(int.MaxValue)]
    public void CertifiedRoundingMatchesIndependentExactNumerator(int period)
    {
        var random = new Random(196707 + period % 100);
        int accepted = 0;
        foreach (int exponent in new[] { -400, -50, 0, 50, 400 })
        for (int i = 0; i < 1000; i++)
        {
            double prior = Math.ScaleB(random.NextDouble() * 2 - 1, exponent);
            double input = Math.ScaleB(random.NextDouble() * 2 - 1, exponent);
            var exact = new ExactMeanAccumulator();
            exact.Add(prior, period - 1L); exact.Add(input, 2);
            double expected = exact.Mean((long)period + 1);
            if (!CertifiedTillson.TrySmoothFloating(prior, input, period, out double actual)) continue;
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
            accepted++;
        }
        Assert.True(accepted > 2000, $"Only {accepted} certified results for period {period}");
    }

    [Fact]
    public void MidpointsCancellationAndRangeEdgesRetainExactFallback()
    {
        double[] values = [0d, -0d, double.Epsilon, -double.Epsilon,
            Math.ScaleB(1, -1022), Math.ScaleB(1, -450), Math.ScaleB(1, 450),
            double.MaxValue, -double.MaxValue, 1d, -1d, Math.BitIncrement(1d), Math.BitDecrement(1d)];
        foreach (int period in new[] { 1, 2, 3, 20, int.MaxValue })
        foreach (double prior in values)
        foreach (double input in values)
        {
            var exact = new ExactMeanAccumulator();
            exact.Add(prior, period - 1L); exact.Add(input, 2);
            double expected = exact.Mean((long)period + 1);
            if (CertifiedTillson.TrySmoothFloating(prior, input, period, out double actual))
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
            if (CertifiedTillson.TrySmooth(prior, input, period, out double combined))
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(combined));
        }
        Assert.False(CertifiedTillson.TrySmoothFloating(1, Math.BitIncrement(1), 3, out _));
        Assert.False(CertifiedTillson.TrySmoothFloating(1, -1, 3, out _));
        Assert.False(CertifiedTillson.TrySmoothFloating(double.Epsilon, 1, 20, out _));
    }
}

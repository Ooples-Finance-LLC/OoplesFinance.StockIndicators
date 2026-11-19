using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class EnvelopeBandCertificateTests
{
    private static (double Upper, double Lower) Exact(double center, double percent)
    {
        var upper = new ExactMeanAccumulator();
        upper.Add(center, 100);
        upper.AddProduct(center, percent);
        var lower = new ExactMeanAccumulator();
        lower.Add(center, 100);
        lower.AddProduct(center, percent, -1);
        return (upper.Mean(100), lower.Mean(100));
    }

    private static bool Check(double center, double percent)
    {
        var certificate = new WindowAverageEnvelope.BandCertificate(percent);
        if (!certificate.TryPublish(center, out var upper, out var lower)) return false;
        var expected = Exact(center, percent);
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Upper), BitConverter.DoubleToInt64Bits(upper));
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Lower), BitConverter.DoubleToInt64Bits(lower));
        return true;
    }

    [Fact]
    public void AdjacentRetryCorrectsBothOrdinaryRoundedProducts()
    {
        double center = Math.BitIncrement(1d);
        var certificate = new WindowAverageEnvelope.BandCertificate(2.5);
        Assert.True(certificate.TryPublish(center, out var upper, out var lower));
        var expected = Exact(center, 2.5);
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Upper), BitConverter.DoubleToInt64Bits(upper));
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Lower), BitConverter.DoubleToInt64Bits(lower));
        Assert.NotEqual(BitConverter.DoubleToInt64Bits(center * 102.5 / 100), BitConverter.DoubleToInt64Bits(upper));
        Assert.NotEqual(BitConverter.DoubleToInt64Bits(center * 97.5 / 100), BitConverter.DoubleToInt64Bits(lower));
    }

    [Fact]
    public void TiesUnsupportedCoefficientsAndUnsafeProductsUseFallback()
    {
        Assert.False(new WindowAverageEnvelope.BandCertificate(200)
            .TryPublish(Math.BitIncrement(1d), out _, out _));
        foreach (double percent in new[] { double.Epsilon, Math.BitDecrement(100d), Math.BitIncrement(100d), double.MaxValue })
            Assert.False(new WindowAverageEnvelope.BandCertificate(percent).TryPublish(1, out _, out _));
        foreach (double center in new[] { double.Epsilon, -double.Epsilon, double.MaxValue, -double.MaxValue,
                     double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            Assert.False(new WindowAverageEnvelope.BandCertificate(2.5).TryPublish(center, out _, out _));
    }

    [Fact]
    public void ZeroBandsRetainCanonicalZeroForEveryCoefficientSign()
    {
        foreach (double percent in new[] { 2.5, 100, 150, 200 })
        foreach (double center in new[] { 0d, -0d })
        {
            Assert.True(new WindowAverageEnvelope.BandCertificate(percent).TryPublish(center, out var upper, out var lower));
            Assert.Equal(0L, BitConverter.DoubleToInt64Bits(upper));
            Assert.Equal(0L, BitConverter.DoubleToInt64Bits(lower));
        }
    }

    [Fact]
    public void AcceptedBinadeBoundariesMatchExactWeightedSums()
    {
        int accepted = 0, fallback = 0;
        foreach (int exponent in new[] { -1074, -1022, -505, -457, -456, -450, -1, 0, 1, 440, 443, 444, 450, 1023 })
        foreach (int sign in new[] { -1, 1 })
        {
            double value = sign * Math.ScaleB(1d, exponent);
            foreach (double center in new[] { value, Math.BitDecrement(value), Math.BitIncrement(value) })
            foreach (double percent in new[] { double.Epsilon, Math.ScaleB(1d, -20), 2.5, Math.BitDecrement(100d), 100, 150, 200, double.MaxValue })
                if (Check(center, percent)) accepted++; else fallback++;
        }
        Assert.True(accepted > 0);
        Assert.True(fallback > 0);
    }

    [Theory]
    [InlineData(92731)]
    [InlineData(18377)]
    public void AcceptedMixedSignsAndScalesMatchExactWeightedSums(int seed)
    {
        var random = new Random(seed);
        double[] percents = [2.5, 100, 150, 200];
        int accepted = 0, fallback = 0;
        for (int i = 0; i < 5000; i++)
        {
            double center = Math.ScaleB(random.NextDouble() * 2 - 1, random.Next(-1050, 1001));
            double percent = i % 3 == 0
                ? Math.ScaleB(random.NextDouble(), random.Next(-1000, 1001))
                : percents[i % percents.Length];
            if (Check(center, percent)) accepted++; else fallback++;
        }
        Assert.True(accepted > 0);
        Assert.True(fallback > 0);
    }
}

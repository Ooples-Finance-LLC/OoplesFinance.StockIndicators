using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class MultiDepthCertifiedTests
{
    private static void Equal((double One, double Two, double Three, Signal Trade) expected,
        (double One, double Two, double Three, Signal Trade) actual)
    {
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected.One), BitConverter.DoubleToInt64Bits(actual.One));
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Two), BitConverter.DoubleToInt64Bits(actual.Two));
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Three), BitConverter.DoubleToInt64Bits(actual.Three));
        Assert.Equal(expected.Trade, actual.Trade);
    }
    private static double Price(int i) => 100 + 13 * Math.Sin(i * .173) + 7 * Math.Cos(i * .317) + (i % 17) * .125;

    [Fact]
    public void TenThousandBarsPreserveEveryBitAndSignalAcrossCertifiedBlocks()
    {
        var actual = new MultiDepthWindow(50); var exact = new MultiDepthWindow(50, allowCertified: false);
        var certified = 0;
        for (var i = 0; i < 10_000; i++)
        {
            if (i % 137 == 0)
            {
                Equal(exact.Next(Price(i) + 9, false), actual.Next(Price(i) + 9, false));
                Equal(exact.Next(Price(i) - 7, false), actual.Next(Price(i) - 7, false));
            }
            Equal(exact.Next(Price(i), true), actual.Next(Price(i), true));
            if (actual.UsingCertifiedPath) certified++;
        }
        Assert.True(certified > 9_900, $"Only {certified} bars used certification.");
        Assert.Equal(0, actual.ExactFallbacks);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(64)]
    public void EligibilityBoundariesRetainBitsAcrossSeveralBlocks(int length)
    {
        var actual = new MultiDepthWindow(length); var exact = new MultiDepthWindow(length, allowCertified: false);
        for (var i = 0; i < 17 * length + 8; i++) Equal(exact.Next(Price(i), true), actual.Next(Price(i), true));
        Assert.True(actual.UsingCertifiedPath);
        Assert.Equal(0, actual.ExactFallbacks);
    }

    [Fact]
    public void AmbiguousImpulsePreviewReplaysOnlyCommittedPrices()
    {
        var actual = new MultiDepthWindow(5); var exact = new MultiDepthWindow(5, allowCertified: false);
        foreach (var value in new[] { 0d, 0, 0, 1, 0, 0, 0, 0 })
            Equal(exact.Next(value, true), actual.Next(value, true));
        Assert.True(actual.UsingCertifiedPath);
        var expected = exact.Next(0, false);
        Assert.Equal(0d, expected.One); // Exact closed-form zero at t = period.
        Equal(expected, actual.Next(0, false));
        Assert.Equal(1, actual.ExactFallbacks);
        Assert.False(actual.UsingCertifiedPath);
        Equal(exact.Next(3, true), actual.Next(3, true));
        for (var i = 0; i < 20; i++) Equal(exact.Next(Price(i), true), actual.Next(Price(i), true));
    }

    [Fact]
    public void ResetRestoresEligibilityAndInvalidPreviewsCannotAdvanceState()
    {
        var actual = new MultiDepthWindow(5); var exact = new MultiDepthWindow(5, allowCertified: false);
        for (var pass = 0; pass < 2; pass++)
        {
            for (var i = 0; i < 90; i++)
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => actual.Next(double.NaN, false));
                Equal(exact.Next(Price(i) + 4, false), actual.Next(Price(i) + 4, false));
                Equal(exact.Next(Price(i), true), actual.Next(Price(i), true));
            }
            Assert.True(actual.UsingCertifiedPath);
            actual.Reset(); exact.Reset();
            Assert.False(actual.UsingCertifiedPath);
            Assert.Equal(0, actual.ExactFallbacks);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(65)]
    [InlineData(int.MaxValue)]
    public void UnsupportedPeriodsKeepTheExactRoute(int length)
    {
        var actual = new MultiDepthWindow(length); var exact = new MultiDepthWindow(length, allowCertified: false);
        for (var i = 0; i < 12; i++) Equal(exact.Next(Price(i), true), actual.Next(Price(i), true));
        Assert.False(actual.UsingCertifiedPath);
        Assert.Equal(0, actual.ExactFallbacks);
    }

    [Theory]
    [InlineData(double.MaxValue)]
    [InlineData(double.Epsilon)]
    [InlineData(-double.MaxValue)]
    public void ConstantsRetainExactUnityAndZeroMargins(double price)
    {
        var actual = new MultiDepthWindow(50); var exact = new MultiDepthWindow(50, allowCertified: false);
        for (var i = 0; i < 410; i++) Equal(exact.Next(price, true), actual.Next(price, true));
        Assert.True(actual.UsingCertifiedPath);
    }
}

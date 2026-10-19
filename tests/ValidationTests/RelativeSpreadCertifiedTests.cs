using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class RelativeSpreadCertifiedTests
{
    private static double Price(int i) => 100 + 13 * Math.Sin(i * .173) + 7 * Math.Cos(i * .317) + (i % 17) * .125;
    private static void Same(double expected, double actual) =>
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));

    [Theory]
    [InlineData(MovingAvgType.ExponentialMovingAverage, 10, 40, 14, 5)]
    [InlineData(MovingAvgType.WildersSmoothingMethod, 3, 7, 5, 4)]
    [InlineData(MovingAvgType.ExponentialMovingAverage, 1, 3, 1, 1)]
    [InlineData(MovingAvgType.WildersSmoothingMethod, 1, int.MaxValue, int.MaxValue, int.MaxValue)]
    [InlineData(MovingAvgType.ExponentialMovingAverage, 5, 5, 14, 3)]
    [InlineData(MovingAvgType.WildersSmoothingMethod, 7, 7, 14, 3)]
    [InlineData(MovingAvgType.ExponentialMovingAverage, 0, -3, -1, 0)]
    public void CertifiedOutputsAndPreviewsMatchForcedExact(MovingAvgType kind, int fast, int slow, int period, int smooth)
    {
        using var actual = new RelativeSpreadKernel(kind, fast, slow, period, smooth);
        using var exact = new RelativeSpreadKernel(kind, fast, slow, period, smooth, false);
        for (var i = 0; i < 512; i++)
        {
            if (i % 17 == 0)
            {
                Same(exact.Next(Price(i) + .25, false), actual.Next(Price(i) + .25, false));
                Same(exact.Next(Price(i) - .5, false), actual.Next(Price(i) - .5, false));
            }
            Same(exact.Next(Price(i), true), actual.Next(Price(i), true));
        }
        Assert.True(actual.CertifiedCount > 100);
    }

    [Fact]
    public void TenThousandCommittedBarsMatchExactWithoutReplay()
    {
        // Dyadic coefficients retain an independent exact 10k-bar oracle at a
        // practical cost; default odd denominators are covered above and by the numerical suite.
        using var actual = new RelativeSpreadKernel(MovingAvgType.ExponentialMovingAverage, 1, 3, 4, 3);
        using var exact = new RelativeSpreadKernel(MovingAvgType.ExponentialMovingAverage, 1, 3, 4, 3, false);
        for (var i = 0; i < 10000; i++) Same(exact.Next(Price(i), true), actual.Next(Price(i), true));
        Assert.Equal(10000, actual.CertifiedCount);
        Assert.Equal(0, actual.FallbackCount);
    }

    [Fact]
    public void AmbiguousPreviewReplaysOnlyCommittedPricesAndResetRestoresCertification()
    {
        using var actual = new RelativeSpreadKernel(MovingAvgType.ExponentialMovingAverage, 1, 3, 4, 3);
        using var exact = new RelativeSpreadKernel(MovingAvgType.ExponentialMovingAverage, 1, 3, 4, 3, false);
        for (var i = 0; i < 32; i++) Same(exact.Next(Price(i), true), actual.Next(Price(i), true));
        for (var i = 0; i < 400 && actual.FallbackCount == 0; i++)
        {
            Same(exact.Next(100, false), actual.Next(100, false));
            if (actual.FallbackCount != 0) break;
            Same(exact.Next(100, true), actual.Next(100, true));
        }
        Assert.Equal(1, actual.FallbackCount);
        Same(exact.Next(100.25, true), actual.Next(100.25, true));
        actual.Reset(); exact.Reset();
        Assert.Equal(0, actual.FallbackCount); Assert.Equal(0, actual.CertifiedCount);
        for (var i = 0; i < 32; i++) Same(exact.Next(Price(i), true), actual.Next(Price(i), true));
        Assert.Equal(32, actual.CertifiedCount);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(double.MaxValue)]
    public void ConstantEmaPricesKeepCorrelatedZeroSpreadsCertified(double price)
    {
        using var actual = new RelativeSpreadKernel(MovingAvgType.ExponentialMovingAverage, 10, 40, 14, 5);
        using var exact = new RelativeSpreadKernel(MovingAvgType.ExponentialMovingAverage, 10, 40, 14, 5, false);
        for (var i = 0; i < 256; i++) Same(exact.Next(price, true), actual.Next(price, true));
        Assert.Equal(256, actual.CertifiedCount); Assert.Equal(0, actual.FallbackCount);
    }

    [Fact]
    public void ExtremePricesAndRejectedInputsPreserveExactState()
    {
        foreach (var kind in new[] { MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        {
            using var actual = new RelativeSpreadKernel(kind, 2, 5, 3, 2);
            using var exact = new RelativeSpreadKernel(kind, 2, 5, 3, 2, false);
            foreach (var price in new[] { double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon, 0d, 100d })
            {
                foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
                    Assert.Throws<ArgumentOutOfRangeException>(() => actual.Next(invalid, true));
                Same(exact.Next(price, false), actual.Next(price, false));
                Same(exact.Next(price, true), actual.Next(price, true));
            }
        }
    }

    [Fact]
    public void OtherAverageKindsStayOnTheExactPath()
    {
        using var actual = new RelativeSpreadKernel(MovingAvgType.SimpleMovingAverage, 2, 5, 3, 2);
        using var exact = new RelativeSpreadKernel(MovingAvgType.SimpleMovingAverage, 2, 5, 3, 2, false);
        for (var i = 0; i < 32; i++) Same(exact.Next(Price(i), true), actual.Next(Price(i), true));
        Assert.Equal(0, actual.CertifiedCount); Assert.Equal(0, actual.FallbackCount);
    }

    [Fact]
    public void ExplicitSpreadInputFirstRestoresTheExactCommittedState()
    {
        using var actual = new RelativeSpreadKernel(MovingAvgType.ExponentialMovingAverage, 3, 7, 4, 3);
        using var exact = new RelativeSpreadKernel(MovingAvgType.ExponentialMovingAverage, 3, 7, 4, 3, false);
        for (var i = 0; i < 32; i++) Same(exact.Next(Price(i), true), actual.Next(Price(i), true));
        var spread = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number.Of(3.25);
        Same(exact.StrengthFromSpread(spread, true), actual.StrengthFromSpread(spread, true));
        Same(exact.Next(Price(32), true), actual.Next(Price(32), true));
        Assert.Equal(1, actual.FallbackCount);
    }
}

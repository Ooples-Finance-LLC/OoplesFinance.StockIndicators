using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class RenkoPriceNormalizationTests
{
    private static Bar Candle(int day, double close, double volume = 0) =>
        new(DateTime.UnixEpoch.AddDays(day), close, close, close, close, volume);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IntegralSeedPreservesEveryBinaryBrick(bool highLow)
    {
        Bar[] bars = [Candle(0, 100), Candle(1, 100.5, 4)];
        var bricks = RenkoSnapshot.Calculate(bars, .125, highLow).ToArray();
        Assert.Equal(4, bricks.Length);
        for (int i = 0; i < bricks.Length; i++)
        {
            Assert.Equal((double)(100m + .125m * i), bricks[i].Open);
            Assert.Equal((double)(100m + .125m * (i + 1)), bricks[i].Close);
            Assert.Equal(1, bricks[i].Volume);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FractionalNegativeSeedRetainsDecimalRounding(bool highLow)
    {
        Bar[] bars = [Candle(0, -1.2345), Candle(1, -1.7345, 4)];
        var bricks = RenkoSnapshot.Calculate(bars, .125, highLow).ToArray();
        Assert.Equal(4, bricks.Length);
        for (int i = 0; i < bricks.Length; i++)
        {
            Assert.Equal((double)(-1.23m - .125m * i), bricks[i].Open);
            Assert.Equal((double)(-1.23m - .125m * (i + 1)), bricks[i].Close);
            Assert.Equal(1, bricks[i].Volume);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeferredSubnormalReversalRetainsFractionalSeed(bool highLow)
    {
        Bar[] bars = [Candle(0, -1.2345), Candle(1, -1.7345, 4), Candle(2, -1.7345)];
        using var iterator = RenkoSnapshot.Calculate(bars, .125, highLow).GetEnumerator();
        Assert.True(iterator.MoveNext());
        var bricks = new List<RenkoBrick> { iterator.Current };
        bars[2] = Candle(2, double.Epsilon, 36);
        while (iterator.MoveNext()) bricks.Add(iterator.Current);
        Assert.Equal(16, bricks.Count);
        for (int i = 0; i < 4; i++)
        {
            Assert.Equal((double)(-1.23m - .125m * i), bricks[i].Open);
            Assert.Equal((double)(-1.23m - .125m * (i + 1)), bricks[i].Close);
            Assert.Equal(1, bricks[i].Volume);
        }
        for (int i = 0; i < 12; i++)
        {
            Assert.Equal((double)(-1.605m + .125m * i), bricks[i + 4].Open);
            Assert.Equal((double)(-1.48m + .125m * i), bricks[i + 4].Close);
            Assert.Equal(3, bricks[i + 4].Volume);
            Assert.Equal(bars[2].Time, bricks[i + 4].Date);
        }
    }

    [Theory]
    [InlineData(false, 1d)]
    [InlineData(true, 1d)]
    [InlineData(false, -1d)]
    [InlineData(true, -1d)]
    public void SignedCoordinateWidthBoundaryPublishesExactly(bool highLow, double sign)
    {
        // The initial negative coordinate is -2^63 on the existing binary grid.
        Bar[] bars = [Candle(0, sign * 2048), Candle(1, sign * 2050, 2)];
        var bricks = RenkoSnapshot.Calculate(bars, 1, highLow).ToArray();
        Assert.Equal(2, bricks.Length);
        for (int i = 0; i < bricks.Length; i++)
        {
            Assert.Equal(sign * (2048 + i), bricks[i].Open);
            Assert.Equal(sign * (2049 + i), bricks[i].Close);
            Assert.Equal(1, bricks[i].Volume);
        }
    }
}

using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class RenkoEndpointCacheTests
{
    private static Bar Candle(int day, double close, double volume) =>
        new(DateTime.UnixEpoch.AddDays(day), close, close, close, close, volume);

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, -1)]
    [InlineData(true, -1)]
    public void SubUlpBricksAndReversalsRetainExactCoordinates(bool highLow, int sign)
    {
        double origin = sign * Math.ScaleB(1, 53);
        Bar[] bars = [Candle(0, origin, 0), Candle(1, origin + sign * 4, 4),
            Candle(2, origin - sign * 4, 7), Candle(3, origin + sign * 4, 7)];
        var bricks = RenkoSnapshot.Calculate(bars, 1, highLow).ToArray();
        Assert.Equal(18, bricks.Length);
        var exactOrigin = ReferenceFraction.FromDouble(origin);
        int at = 0;
        Check(1, 0, 1, 4);
        Check(2, 3, -1, 7);
        Check(3, -3, 1, 7);

        void Check(int day, int first, int direction, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var brick = bricks[at++];
                int offset = first + direction * i;
                double open = (exactOrigin + new ReferenceFraction(sign * offset)).ToDouble();
                double close = (exactOrigin + new ReferenceFraction(sign * (offset + direction))).ToDouble();
                Assert.Equal(BitConverter.DoubleToInt64Bits(open), BitConverter.DoubleToInt64Bits(brick.Open));
                Assert.Equal(BitConverter.DoubleToInt64Bits(close), BitConverter.DoubleToInt64Bits(brick.Close));
                Assert.Equal(sign * direction > 0, brick.IsUp);
                Assert.Equal(bars[day].Time, brick.Date);
                Assert.Equal(1, brick.Volume);
            }
        }
    }

    [Theory]
    [InlineData(false, double.NaN)]
    [InlineData(true, double.NaN)]
    [InlineData(false, double.PositiveInfinity)]
    [InlineData(true, double.PositiveInfinity)]
    [InlineData(false, double.NegativeInfinity)]
    [InlineData(true, double.NegativeInfinity)]
    public void DeferredVolumeMutationWaitsForPendingBricks(bool highLow, double invalid)
    {
        Bar[] bars = [Candle(0, 0, 0), Candle(1, 2, 2), Candle(2, 3, 1)];
        using var iterator = RenkoSnapshot.Calculate(bars, 1, highLow).GetEnumerator();
        Assert.True(iterator.MoveNext());
        bars[2] = Candle(2, 3, invalid);
        Assert.True(iterator.MoveNext());
        Assert.Equal(1, iterator.Current.Open);
        Assert.Equal(2, iterator.Current.Close);
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => iterator.MoveNext());
        Assert.Equal("value", error.ParamName);
        Assert.Equal(new ArgumentOutOfRangeException("value").Message, error.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeferredPriceMutationWaitsForPendingBricks(bool highLow)
    {
        Bar[] bars = [Candle(0, 0, 0), Candle(1, 2, 2), Candle(2, 3, 1)];
        using var iterator = RenkoSnapshot.Calculate(bars, 1, highLow).GetEnumerator();
        Assert.True(iterator.MoveNext());
        bars[2] = Candle(2, double.NaN, 1);
        Assert.True(iterator.MoveNext());
        Assert.Equal(2, iterator.Current.Close);
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => iterator.MoveNext());
        Assert.Equal("value", error.ParamName);
        Assert.Equal(new ArgumentOutOfRangeException("value").Message, error.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WideCountReusesEndpointsWithoutMaterializingChart(bool highLow)
    {
        double price = Math.ScaleB(1, 100);
        Bar[] bars = [Candle(0, 0, 0), Candle(1, price, price / 3)];
        var bricks = RenkoSnapshot.Calculate(bars, 3, highLow).Take(3).ToArray();
        Assert.Equal(3, bricks.Length);
        for (int i = 0; i < bricks.Length; i++)
        {
            Assert.Equal(3 * i, bricks[i].Open);
            Assert.Equal(3 * (i + 1), bricks[i].Close);
            Assert.Equal(1, bricks[i].Volume);
        }
    }
}

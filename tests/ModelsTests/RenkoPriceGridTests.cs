using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class RenkoPriceGridTests
{
    private static Bar Candle(int day, double close, double volume = 0) =>
        new(DateTime.UnixEpoch.AddDays(day), 0, Math.Max(0, close), Math.Min(0, close), close, volume);

    private static double Coordinate(double origin, double step, int index)
    {
        var exact = new ExactMeanAccumulator();
        exact.Add(origin);
        exact.Add(step, index);
        return exact.Mean(1);
    }

    [Theory]
    [InlineData(1d, false)]
    [InlineData(1d, true)]
    [InlineData(.5, false)]
    [InlineData(.5, true)]
    [InlineData(.1, false)]
    [InlineData(.1, true)]
    [InlineData(1e-200, false)]
    [InlineData(1e-200, true)]
    [InlineData(1e200, false)]
    [InlineData(1e200, true)]
    [InlineData(double.Epsilon, false)]
    [InlineData(double.Epsilon, true)]
    public void ExactBrickBoundariesSurviveNormalAndSubnormalGrids(double step, bool highLow)
    {
        double target = step * 10;
        int count = (int)(ExactVarianceWindow.Units(target) / ExactVarianceWindow.Units(step));
        Bar[] bars = [Candle(0, 0), Candle(1, target, count)];
        var bricks = RenkoSnapshot.Calculate(bars, step, highLow).ToArray();
        Assert.Equal(count, bricks.Length);
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(Coordinate(0, step, i), bricks[i].Open);
            Assert.Equal(Coordinate(0, step, i + 1), bricks[i].Close);
            Assert.Equal(1, bricks[i].Volume);
            Assert.Equal(bars[1].Time, bricks[i].Date);
            Assert.Equal(target, bricks[i].High);
            Assert.Equal(0, bricks[i].Low);
            Assert.True(bricks[i].IsUp);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FinerNegativePriceReadAfterYieldExpandsRetainedCoordinatesExactly(bool highLow)
    {
        Bar[] bars = [Candle(0, 10), Candle(1, 12, 4), Candle(2, 12)];
        // Keep the high/low source on the same positive excursion as close.
        bars[0] = new(bars[0].Time, 10, 10, 10, 10, 0);
        bars[1] = new(bars[1].Time, 10, 12, 10, 12, 4);
        using var iterator = RenkoSnapshot.Calculate(bars, 1, highLow).GetEnumerator();
        Assert.True(iterator.MoveNext());
        Assert.Equal(11, iterator.Current.Close);
        bars[2] = Candle(2, Math.BitDecrement(-2d), 39);
        Assert.True(iterator.MoveNext());
        Assert.Equal(12, iterator.Current.Close);
        var tail = new List<RenkoBrick>();
        while (iterator.MoveNext()) tail.Add(iterator.Current);
        Assert.Equal(13, tail.Count);
        for (int i = 0; i < tail.Count; i++)
        {
            Assert.Equal(11 - i, tail[i].Open);
            Assert.Equal(10 - i, tail[i].Close);
            Assert.Equal(3, tail[i].Volume);
            Assert.False(tail[i].IsUp);
        }
    }

    [Fact]
    public void DecimalSeedAndNonBinaryBrickSizeKeepOriginalSingleRounding()
    {
        Bar[] bars = [Candle(0, 1.25), Candle(1, 1.5, 4)];
        var bricks = RenkoSnapshot.Calculate(bars, .1).ToArray();
        Assert.Equal(4, bricks.Length);
        for (int i = 0; i < bricks.Length; i++)
        {
            Assert.Equal(Coordinate(1, .1, i), bricks[i].Open);
            Assert.Equal(Coordinate(1, .1, i + 1), bricks[i].Close);
            Assert.Equal(1, bricks[i].Volume);
        }
    }
}

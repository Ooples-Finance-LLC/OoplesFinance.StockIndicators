using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class RenkoNormalizedGridTests
{
    private static Bar Candle(int day, double close, double volume = 0) =>
        new(DateTime.UnixEpoch.AddDays(day), close, close, close, close, volume);

    [Theory]
    [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, -1)] [InlineData(true, -1)]
    public void SmallIntegerPromotionAndPausedReversalRetainEveryBrick(bool highLow, int sign)
    {
        double origin = sign * (Math.ScaleB(1, 31) - 2);
        Bar[] bars = [Candle(0, origin), Candle(1, origin + 4 * sign, 16), Candle(2, origin)];
        using var iterator = RenkoSnapshot.Calculate(bars, 1, highLow).GetEnumerator();
        Assert.True(iterator.MoveNext());
        var bricks = new List<RenkoBrick> { iterator.Current };
        // The current candle was already consumed. Future candles remain lazy.
        bars[1] = Candle(1, double.NaN, double.NaN);
        bars[2] = Candle(2, origin - 4 * sign, 7);
        while (iterator.MoveNext()) bricks.Add(iterator.Current);
        Assert.Equal(11, bricks.Count);
        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(origin + sign * i, bricks[i].Open);
            Assert.Equal(origin + sign * (i + 1), bricks[i].Close);
            Assert.Equal(4, bricks[i].Volume);
            Assert.Equal(sign > 0, bricks[i].IsUp);
            Assert.Equal(bars[1].Time, bricks[i].Date);
        }
        for (int i = 0; i < 7; i++)
        {
            Assert.Equal(origin + sign * (3 - i), bricks[i + 4].Open);
            Assert.Equal(origin + sign * (2 - i), bricks[i + 4].Close);
            Assert.Equal(1, bricks[i + 4].Volume);
            Assert.Equal(sign < 0, bricks[i + 4].IsUp);
            Assert.Equal(bars[2].Time, bricks[i + 4].Date);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CoordinatesBeyondSignedWordRemainExactAndLazy(bool highLow)
    {
        foreach (int sign in new[] { -1, 1 })
        {
            double origin = sign * Math.ScaleB(1, 63);
            double target = sign * Math.BitIncrement(Math.Abs(origin));
            Bar[] bars = [Candle(0, origin), Candle(1, target, 2048)];
            var first = RenkoSnapshot.Calculate(bars, 1, highLow).Take(3).ToArray();
            Assert.Equal(3, first.Length);
            for (int i = 0; i < first.Length; i++)
            {
                var open = ReferenceFraction.FromDouble(origin) + new ReferenceFraction(sign * i);
                var close = ReferenceFraction.FromDouble(origin) + new ReferenceFraction(sign * (i + 1));
                Assert.Equal(BitConverter.DoubleToInt64Bits(open.ToDouble()), BitConverter.DoubleToInt64Bits(first[i].Open));
                Assert.Equal(BitConverter.DoubleToInt64Bits(close.ToDouble()), BitConverter.DoubleToInt64Bits(first[i].Close));
                Assert.Equal(1, first[i].Volume);
                Assert.Equal(sign > 0, first[i].IsUp);
            }
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void EvenSubnormalStepPreservesUnboundedCountAndExactVolumeShare(bool highLow)
    {
        double step = 2 * double.Epsilon;
        double target = Math.ScaleB(1, -970);
        double count = Math.ScaleB(1, 103);
        Bar[] bars = [Candle(0, 0), Candle(1, target, count)];
        var first = RenkoSnapshot.Calculate(bars, step, highLow).Take(3).ToArray();
        Assert.Equal(3, first.Length);
        for (int i = 0; i < first.Length; i++)
        {
            Assert.Equal(i * step, first[i].Open);
            Assert.Equal((i + 1) * step, first[i].Close);
            Assert.Equal(1, first[i].Volume);
            Assert.True(first[i].IsUp);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DecimalSeedGcdAndRefinedBinaryGridPreserveSingleRounding(bool highLow)
    {
        Bar[] bars = [Candle(0, 1.23), Candle(1, 6.2, 4)];
        var bricks = RenkoSnapshot.Calculate(bars, 1.25, highLow).ToArray();
        Assert.Equal(4, bricks.Length);
        var seed = new ReferenceFraction(6) / new ReferenceFraction(5);
        var step = new ReferenceFraction(5) / new ReferenceFraction(4);
        for (int i = 0; i < bricks.Length; i++)
        {
            Assert.Equal((seed + step * new ReferenceFraction(i)).ToDouble(), bricks[i].Open);
            Assert.Equal((seed + step * new ReferenceFraction(i + 1)).ToDouble(), bricks[i].Close);
            Assert.Equal(1, bricks[i].Volume);
        }
    }
}

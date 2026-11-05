using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class RenkoCompactVolumeTests
{
    private static Bar Candle(int day, double close, double volume) =>
        new(DateTime.UnixEpoch.AddDays(day), 0, Math.Max(0, close), Math.Min(0, close), close, volume);

    [Theory]
    [InlineData(false, 1d)]
    [InlineData(true, 1d)]
    [InlineData(false, -1d)]
    [InlineData(true, -1d)]
    public void MixedExponentCancellationRetainsSubnormalVolume(bool highLow, double sign)
    {
        Bar[] bars = [Candle(0, 0, 0), Candle(1, 0, sign * double.MaxValue),
            Candle(2, 0, sign * double.Epsilon), Candle(3, 1, -sign * double.MaxValue)];
        var brick = Assert.Single(RenkoSnapshot.Calculate(bars, 1, highLow));
        Assert.Equal(BitConverter.DoubleToInt64Bits(sign * double.Epsilon),
            BitConverter.DoubleToInt64Bits(brick.Volume));
        Assert.Equal(bars[3].Time, brick.Date);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WideOddBrickCountSharesFiniteVolumeLazily(bool highLow)
    {
        // floor(2^100 / 3) is an odd 99-bit count. The exact share rounds to 1.
        double price = Math.ScaleB(1, 100);
        Bar[] bars = [Candle(0, 0, 0), Candle(1, price, price / 3)];
        var first = RenkoSnapshot.Calculate(bars, 3, highLow).First();
        Assert.Equal(3, first.Close);
        Assert.Equal(1, first.Volume);
        Assert.Equal(bars[1].Time, first.Date);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void DeferredVolumeMutationKeepsOriginalUnitsException(double invalid)
    {
        Bar[] bars = [Candle(0, 0, 0), Candle(1, 2, 4)];
        var chart = RenkoSnapshot.Calculate(bars);
        bars[1] = Candle(1, 2, invalid);
        using var iterator = chart.GetEnumerator();
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => iterator.MoveNext());
        Assert.Equal("value", error.ParamName);
        Assert.Equal(new ArgumentOutOfRangeException("value").Message, error.Message);
    }
}

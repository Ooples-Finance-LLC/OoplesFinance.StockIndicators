using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.HelpersTests;

public sealed class BartlettVolumeWindowTests
{
    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(-3)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(20)]
    [InlineData(101)]
    [InlineData(4096)]
    [InlineData(int.MaxValue)]
    public void RollingMomentsMatchFrozenTapLoopAcrossPreviewsAndResets(int period)
    {
        using var window = new BartlettVolumeWindow(period);
        var history = new List<(double Price, double Volume)>();
        void Check(double price, double volume, bool final)
        {
            var expected = FrozenTapLoop(history, price, volume, period);
            var actual = window.Next(price, volume, final);
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
            if (final) history.Add((price, volume));
        }
        for (int replay = 0; replay < 2; replay++)
        {
            window.Reset(); history.Clear();
            for (int i = 0; i < 257; i++)
            {
                Check(i % 7 - 3d, i % 5 - 2d, false);
                Check(i % 19 / 8d - 1, i % 13 - 6d, true);
            }
            double[] extremes = [0d, -0d, double.Epsilon, -double.Epsilon, double.MaxValue, -double.MaxValue, 1d, -1d];
            for (int i = 0; i < 64; i++)
            {
                Check(extremes[i % 8], extremes[(i * 3 + 1) % 8], false);
                Check(extremes[i % 8], extremes[(i * 3 + 1) % 8], true);
            }
        }
    }

    // Original core tap definition at 03b11759; preview appended without mutation.
    private static double FrozenTapLoop(List<(double Price, double Volume)> history, double price, double volume, int period)
    {
        period = Math.Max(1, period);
        var mean = new ExactVolumeMean();
        for (int lag = 0; lag < period && lag <= history.Count; lag++)
        {
            var point = lag == 0 ? (Price: price, Volume: volume) : history[history.Count - lag];
            mean.Add(point.Price, point.Volume, period == 1 ? 1 : Math.Min(lag, period - lag));
        }
        return mean.Value();
    }
}

using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.HelpersTests;

public sealed class BetterVolumeValueOnlyTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(8, 2)]
    [InlineData(20, 7)]
    [InlineData(int.MinValue, int.MaxValue)]
    [InlineData(int.MaxValue, 1)]
    public void ValuesAndDefaultSignalsMatchFrozenKernelAcrossPreviewsAndReset(int period, int lookback)
    {
        var frozen = new PreviousBetterVolumeWindow(period, lookback);
        var full = new BetterVolumeWindow(period, lookback);
        var values = new BetterVolumeWindow(period, lookback, includeSignals: false);
        void Check(double price, double volume, bool commit)
        {
            double open = price / 4, high = Math.Abs(price), low = -Math.Abs(price) / 2;
            var expected = frozen.Next(open, high, low, price, volume, commit);
            var complete = full.Next(open, high, low, price, volume, commit);
            var actual = values.Next(open, high, low, price, volume, commit);
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Value), BitConverter.DoubleToInt64Bits(complete.Value));
            Assert.Equal(expected.Signal, complete.Signal);
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Value), BitConverter.DoubleToInt64Bits(actual.Value));
            Assert.Equal(Signal.None, actual.Signal);
        }
        for (int replay = 0; replay < 2; replay++)
        {
            frozen.Reset(); full.Reset(); values.Reset();
            for (int i = 0; i < 129; i++)
            {
                Check(i % 17 - 8, i % 7 - 3, false);
                Check(i % 11 - 5, i % 5, false);
                Check(i % 11 - 5, i % 5, true);
            }
            foreach (var price in new[] { 0d, -0d, double.Epsilon, -double.Epsilon, double.MaxValue, -double.MaxValue, 1d, 2, 3 })
            foreach (var volume in new[] { 0d, double.Epsilon, double.MaxValue, -double.MaxValue })
            {
                Check(price, volume, false);
                Check(price, volume, true);
            }
        }
    }
}

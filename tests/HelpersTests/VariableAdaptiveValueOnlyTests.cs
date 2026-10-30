using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.HelpersTests;

public sealed class VariableAdaptiveValueOnlyTests
{
    public static IEnumerable<object[]> Cases =>
        from period in new[] { 1, 2, 7, 20, 4096 }
        from kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod }
        select new object[] { period, kind };

    [Theory, MemberData(nameof(Cases))]
    public void ValuesAndLaterSignalsMatchFrozenKernelAcrossPreviewsAndReset(int period, MovingAvgType kind)
    {
        using var expected = new PreviousVariableAdaptiveWindow(kind, period);
        using var actual = new VariableAdaptiveWindow(kind, period);
        void Check(double price, bool final, bool signals)
        {
            var open = price / 4; var high = Math.Abs(price) + 1; var low = -Math.Abs(price) / 2 - 2;
            var bar = new OhlcvBar("VALUE", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, open, high, low, price, 1, true);
            var left = expected.Next(bar, final);
            var right = signals ? actual.Next(bar, final) : actual.NextValidated(price, open, high, low, final, includeSignals: false);
            Assert.Equal(BitConverter.DoubleToInt64Bits(left.Value), BitConverter.DoubleToInt64Bits(right.Value));
            if (signals) Assert.Equal(left.Trade, right.Trade);
        }
        for (int replay = 0; replay < 2; replay++)
        {
            expected.Reset(); actual.Reset();
            for (int i = 0; i < 80; i++)
            {
                Check(7 - i % 17, false, false);
                Check(i % 11 - 5, false, true);
                Check(i % 11 - 5, true, i % 3 == 0);
            }
            foreach (var price in new[] { 0d, -0d, double.Epsilon, -double.Epsilon, double.MaxValue, -double.MaxValue, 1d, 2, 3 })
            {
                Check(price, false, false);
                Check(price, true, false);
                Check(2, false, true);
            }
        }
    }
}

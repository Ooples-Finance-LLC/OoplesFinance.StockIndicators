using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.HelpersTests;

public sealed class SymmetricRecurrenceTests
{
    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(20)]
    [InlineData(21)]
    [InlineData(530)]
    [InlineData(531)]
    [InlineData(4096)]
    [InlineData(int.MaxValue)]
    public void ExactRecurrenceMatchesUnchangedTapReferenceAndPreservesState(int period)
    {
        int count = (int)Math.Max(65, Math.Min(1100, 2L * Math.Max(1, period) + 7));
        var prices = Enumerable.Range(0, count).Select(i => i % 13 == 0 ? -0d :
            Math.ScaleB(i % 17 - 8d, i % 7 == 0 ? -1022 : i % 11 == 0 ? 900 : 0)).ToArray();
        var expected = new double[count];
        MovingAverageCore.SymmetricallyWeightedMovingAverage(prices, expected, period);
        using var mean = new SymmetricWindowMean(period, count);
        for (int pass = 0; pass < 2; pass++)
        {
            mean.Reset();
            for (int i = 0; i < count; i++)
            {
                mean.Next(-double.MaxValue, false);
                if (i == 0 || i == count / 2 || i == count - 1)
                {
                    Assert.Throws<ArithmeticException>(() => mean.Next(double.NaN, true));
                    Assert.Throws<ArithmeticException>(() => mean.Next(double.PositiveInfinity, false));
                }
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i]), BitConverter.DoubleToInt64Bits(mean.Next(prices[i], false)));
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i]), BitConverter.DoubleToInt64Bits(mean.Next(prices[i], true)));
            }
        }
    }
}

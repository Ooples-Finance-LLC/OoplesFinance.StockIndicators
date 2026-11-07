using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.HelpersTests;

public sealed class ImpulseResponseRingTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    [InlineData(740)]
    [InlineData(int.MaxValue)]
    public void WrappedHistoryPreservesTapOrderPreviewsExtendedOutputAndReset(int length)
    {
        int capacity = (int)Math.Max(2, Math.Min(530, Math.Ceiling(length / 1.4)));
        using var expected = new PreviousImpulseResponseWindow(MovingAvgType.EhlersHannMovingAverage, length, 1);
        using var actual = new ImpulseResponseWindow(MovingAvgType.EhlersHannMovingAverage, length, 1);
        for (int pass = 0; pass < 2; pass++)
        {
            expected.Reset(); actual.Reset();
            for (int i = 0; i < capacity * 2 + 9; i++)
            {
                double price = Math.ScaleB(i % 19 - 9d, i % 7 == 0 ? -1022 : i % 11 == 0 ? 900 : 0);
                Compare(-price, false);
                Compare(price, false);
                Compare(price, true);
            }
        }

        void Compare(double price, bool commit)
        {
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Next(price, commit, true)),
                BitConverter.DoubleToInt64Bits(actual.Next(price, commit, true)));
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected.ExtendedOutput.Mantissa),
                BitConverter.DoubleToInt64Bits(actual.ExtendedOutput.Mantissa));
            Assert.Equal(expected.ExtendedOutput.UpperShift, actual.ExtendedOutput.UpperShift);
        }
    }
}

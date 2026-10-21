using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class KalmanRationalRootTests
{
    [Theory]
    [InlineData(50)]
    [InlineData(200)]
    [InlineData(800)]
    [InlineData(1250)]
    [InlineData(1)]
    [InlineData(17)]
    public void ExactSpecializationPreservesBitsSignalsPreviewAndReset(int length)
    {
        var candidate = new KalmanSmootherWindow(length);
        var preserved = new KalmanSmootherWindow(length, false);
        void Compare(double price, bool final)
        {
            var expected = preserved.Next(price, final);
            var actual = candidate.Next(price, final);
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Line), BitConverter.DoubleToInt64Bits(actual.Line));
            Assert.Equal(expected.Trade, actual.Trade);
        }
        for (var pass = 0; pass < 2; pass++)
        {
            for (var i = 0; i < 80; i++)
            {
                var price = i % 7 switch { 0 => 1e150, 1 => -1e150, 2 => double.Epsilon,
                    3 => -double.Epsilon, 4 => -0d, _ => 10 + (i * 13 % 101) / 8d };
                Compare(-price, false); Compare(price, false); Compare(price, true);
            }
            candidate.Reset(); preserved.Reset();
        }
    }
}

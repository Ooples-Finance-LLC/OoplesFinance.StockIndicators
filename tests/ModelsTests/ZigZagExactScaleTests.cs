using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class ZigZagExactScaleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CommonAndWideExponentGridsPreserveEveryOutputBit(bool highLow)
    {
        foreach (int exponent in new[] { -1074, -1022, -450, 0, 450, 1020 })
        {
            var bars = Enumerable.Range(0, 97).Select(i =>
            {
                double price = Math.ScaleB(1.25 + Math.Sin(i * .71) * .2, exponent);
                return new Bar(default, price, price * 1.1, price * .9, price, 1);
            }).ToArray();
            foreach (double percent in new[] { double.Epsilon, Math.BitDecrement(5), 5d, Math.BitIncrement(5), 100, double.MaxValue })
                Check(bars, percent, highLow);
        }
        double[] wide = [0, -0d, double.Epsilon, -double.Epsilon,
            double.MaxValue, -double.MaxValue, 1, -1, 100, 101, 90];
        Check(Enumerable.Range(0, 99).Select(i =>
        {
            double price = wide[i % wide.Length];
            return new Bar(default, price, price, price, price, 1);
        }).ToArray(), 5, highLow);
    }

    [Fact]
    public void InitialCloseConstrainsTheGridInHighLowMode()
    {
        var bars = Enumerable.Range(0, 31).Select(i =>
            new Bar(default, 100, 110, 90, i == 0 ? double.Epsilon : 100, 1)).ToArray();
        Check(bars, double.MaxValue, true);
        Check(bars.Select(b => new Bar(b.Time, b.Open, 0, -0d, b.Close, b.Volume)).ToArray(), 5, true);
    }

    private static void Check(Bar[] bars, double percent, bool highLow)
    {
        var expected = OriginalZigZagSnapshot.Calculate(bars, percent, highLow);
        var actual = ZigZagSnapshot.Calculate(bars, percent, highLow);
        Assert.Equal(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            Same(expected[i].ZigZag, actual[i].ZigZag);
            Same(expected[i].RetraceHigh, actual[i].RetraceHigh);
            Same(expected[i].RetraceLow, actual[i].RetraceLow);
            Assert.Equal(expected[i].PointType, actual[i].PointType);
        }
    }

    private static void Same(double? expected, double? actual)
    {
        Assert.Equal(expected.HasValue, actual.HasValue);
        if (expected.HasValue)
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Value),
                BitConverter.DoubleToInt64Bits(actual!.Value));
    }
}

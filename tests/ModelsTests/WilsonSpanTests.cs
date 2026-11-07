using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class WilsonSpanTests
{
    public static IEnumerable<object[]> Cases =>
        from kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage }
        from length in new[] { 1, 3, int.MaxValue }
        select new object[] { kind, length };

    [Theory, MemberData(nameof(Cases))]
    public void SpanPreservesExactLinesSignalsPreviewAndReset(MovingAvgType kind, int length)
    {
        using var reference = new WilsonWindow(kind, length, 3, 30, 45, 70, 55);
        using var candidate = new WilsonWindow(kind, length, 3, 30, 45, 70, 55);
        double[] lines = new double[5];
        for (int repeat = 0; repeat < 2; repeat++)
        {
            foreach (var price in new[] { -0d, 0d, double.Epsilon, -double.Epsilon, 1d, 2d, 2d, 2d, -1d, -1d, 100d, 0d })
            {
                foreach (var final in new[] { false, false, true })
                {
                    lines[4] = 123;
                    var expected = reference.Next(price, final);
                    var signal = candidate.Next(price, final, lines);
                    Assert.Equal(expected.Trade, signal);
                    Assert.Equal(expected.Lines.Select(BitConverter.DoubleToInt64Bits), lines.Take(4).Select(BitConverter.DoubleToInt64Bits));
                    Assert.Equal(123, lines[4]);
                }
            }
            reference.Reset(); candidate.Reset();
        }
    }

    [Fact]
    public void ShortDestinationAndInvalidPriceDoNotAdvanceState()
    {
        using var reference = new WilsonWindow(MovingAvgType.ExponentialMovingAverage, 3, 2, 30, 45, 70, 55);
        using var candidate = new WilsonWindow(MovingAvgType.ExponentialMovingAverage, 3, 2, 30, 45, 70, 55);
        Assert.Throws<ArgumentException>(() => candidate.Next(100, true, new double[3]));
        Assert.Throws<ArgumentOutOfRangeException>(() => candidate.Next(double.NaN, true, new double[4]));
        double[] lines = new double[4];
        foreach (var price in new[] { 2d, 1d, 3d })
        {
            var expected = reference.Next(price, true);
            Assert.Equal(expected.Trade, candidate.Next(price, true, lines));
            Assert.Equal(expected.Lines.Select(BitConverter.DoubleToInt64Bits), lines.Select(BitConverter.DoubleToInt64Bits));
        }
    }
}

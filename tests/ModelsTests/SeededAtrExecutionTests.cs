using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SeededAtrExecutionTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(20)]
    [InlineData(int.MaxValue)]
    public void CompactRangeStagesMatchIndependentReferenceAndReset(int period)
    {
        var bars = new[]
        {
            new Bar(default, 0, 10, -10, 0, 1),
            new Bar(default, 0, double.MaxValue, -double.MaxValue, 1, 1),
            new Bar(default, 0, 1, -1, -double.MaxValue, 1),
            new Bar(default, 0, double.MaxValue, double.MaxValue, 2, 1),
            new Bar(default, 0, -5, 7, 0, 1), // inverted candle retains the general max expression
            new Bar(default, 0, double.Epsilon, -double.Epsilon, double.Epsilon, 1),
            new Bar(default, 0, -0d, 0d, -0d, 1)
        };
        var window = new SeededAtrWindow(period);
        foreach (var input in new[] { bars, bars.Reverse().ToArray(), Enumerable.Repeat(bars[0], 51).ToArray() })
        {
            var expected = SeededAtrReference.Values(input, period);
            window.Reset();
            for (int i = 0; i < input.Length; i++)
            {
                window.Add(input[i]);
                Bits(expected[0][i], window.Range.Publish());
                Bits(expected[1][i], window.Average.Publish());
                Bits(expected[2][i], window.Percent(input[i].Close));
                Assert.Equal(expected[3][i] != 0, window.HasRange);
                Assert.Equal(expected[4][i] != 0, window.HasAverage);
            }
        }
    }

    [Fact]
    public void OrdinaryStreamingRangesAndSmoothingAllocateNoPerBarObjects()
    {
        var bars = Enumerable.Range(0, 97).Select(i => new Bar(default, 0,
            100 + i / 8d, 98 + i / 8d, 99 + i / 8d, 1)).ToArray();
        var window = new SeededAtrWindow(20);
        for (int i = 0; i < 1000; i++) window.Add(bars[i % bars.Length]);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 4000; i++) window.Add(bars[i % bars.Length]);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(window.Average.Publish() > 0);
    }

    private static void Bits(double expected, double actual) =>
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
}

using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class ZigZagSnapshotSharedExecutionTests(Xunit.Abstractions.ITestOutputHelper log)
{
    private static Bar[] Data(int count, int shape = 0) => Enumerable.Range(0, count).Select(i =>
    {
        double value = shape switch
        {
            1 => i % 4 < 2 ? 100 : 120,
            2 => 100,
            3 => Math.ScaleB(1.25 + Math.Sin(i * .73) * .25, 1023),
            4 => (i % 7 + 1) * double.Epsilon,
            5 => i % 2 == 0 ? 0d : -0d,
            _ => 100 + Math.Sin(i * .71) * 20 + Math.Sin(i * .09)
        };
        return new Bar(DateTime.UnixEpoch.AddMinutes(i), value, value, value * .9, value, 1);
    }).ToArray();

    private static void Same(double? expected, double? actual)
    {
        Assert.Equal(expected.HasValue, actual.HasValue);
        if (expected.HasValue) Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Value), BitConverter.DoubleToInt64Bits(actual!.Value));
    }

    [Theory]
    [InlineData(false, 5)] [InlineData(true, 5)] [InlineData(false, .01)]
    [InlineData(true, 100)] [InlineData(false, double.MaxValue)]
    public void EveryRetrospectiveFieldMatchesOriginal(bool highLow, double percent)
    {
        foreach (var count in new[] { 0, 1, 2, 3, 19, 129 })
        foreach (var shape in Enumerable.Range(0, 6))
        {
            var bars = Data(count, shape);
            var expected = OriginalZigZagSnapshot.Calculate(bars, percent, highLow);
            var actual = ZigZagSnapshot.Calculate(bars, percent, highLow);
            Assert.Equal(expected.Count, actual.Count);
            for (var i = 0; i < expected.Count; i++)
            {
                Same(expected[i].ZigZag, actual[i].ZigZag);
                Same(expected[i].RetraceHigh, actual[i].RetraceHigh);
                Same(expected[i].RetraceLow, actual[i].RetraceLow);
                Assert.Equal(expected[i].PointType, actual[i].PointType);
            }
        }
    }

    [Fact]
    public void ValidationAndOwnedOutputsRemainIntact()
    {
        Assert.Throws<ArgumentNullException>(() => ZigZagSnapshot.Calculate(null!, 0));
        var invalid = new[] { new Bar(DateTime.UnixEpoch, 0, double.NaN, 0, 1, 0) };
        Assert.Single(ZigZagSnapshot.Calculate(invalid));
        Assert.Equal("bars", Assert.Throws<ArgumentOutOfRangeException>(() => ZigZagSnapshot.Calculate(invalid, highLow: true)).ParamName);
        Assert.Equal("percentChange", Assert.Throws<ArgumentOutOfRangeException>(() => ZigZagSnapshot.Calculate(invalid, 0, true)).ParamName);
        var bars = Data(129); var rows = ZigZagSnapshot.Calculate(bars); var saved = rows.ToArray();
        Array.Clear(bars); Assert.Equal(saved, rows);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FinalRowsReduceAllocationInBothPriceModes(bool highLow)
    {
        var bars = Data(1000);
        for (var i = 0; i < 3; i++) { OriginalZigZagSnapshot.Calculate(bars, highLow: highLow); ZigZagSnapshot.Calculate(bars, highLow: highLow); }
        static long Measure(Func<IReadOnlyList<ZigZagValue>> action)
        {
            var start = GC.GetAllocatedBytesForCurrentThread(); var rows = action();
            var size = GC.GetAllocatedBytesForCurrentThread() - start; GC.KeepAlive(rows); return size;
        }
        var original = Measure(() => OriginalZigZagSnapshot.Calculate(bars, highLow: highLow));
        var compact = Measure(() => ZigZagSnapshot.Calculate(bars, highLow: highLow));
        log.WriteLine($"1000bars highLow={highLow} including outputs: original {original}, compact {compact} bytes.");
        Assert.True(compact < original, $"original={original}; compact={compact}");
    }
}

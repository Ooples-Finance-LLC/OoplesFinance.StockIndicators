using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class IchimokuSnapshotExecutionTests(Xunit.Abstractions.ITestOutputHelper log)
{
    private static Bar[] Data(int count) => Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 100, 110 + i % 17, 90 - i % 13, 100 + i % 7, i % 13)).ToArray();
    [Theory]
    [InlineData(9, 26, 52)] [InlineData(1, 1, 1)] [InlineData(3, 3, 7)]
    [InlineData(7, 3, 3)] [InlineData(int.MaxValue, int.MaxValue, int.MaxValue)] [InlineData(3, int.MaxValue, 5)]
    public void CompactColumnsPreserveAllFieldsAndShiftBoundaries(int conversion, int basis, int spanB)
    {
        foreach (int count in new[] { 0, 1, 25, 26, 51, 52, 53, 513, 10000 })
        {
            var bars = Data(count);
            var expected = OriginalIchimokuSnapshot.Calculate(bars, conversion, basis, spanB);
            var actual = IchimokuCloudSnapshot.Calculate(bars, conversion, basis, spanB);
            Rows(expected, actual);
            var indexed = OriginalIchimokuSnapshot.Extended(bars, conversion, basis, spanB, -3, count + 3L).ToArray();
            var result = IchimokuCloudSnapshot.Extended(bars, conversion, basis, spanB, -3, count + 3L).ToArray();
            Assert.Equal(indexed.Select(r => r.Index), result.Select(r => r.Index)); Rows(indexed.Select(r => r.Value), result.Select(r => r.Value));
        }
    }
    [Fact]
    public void ExtremeOffsetsAndLongEndpointsPreserveAbsenceWithoutOverflow()
    {
        var bars = Data(65);
        foreach (int forward in new[] { 0, 1, 26, int.MaxValue })
        foreach (int backward in new[] { 0, 1, 26, int.MaxValue })
            Rows(OriginalIchimokuSnapshot.Calculate(bars, 3, 5, 7, forward, backward), IchimokuCloudSnapshot.Calculate(bars, 3, 5, 7, forward, backward));
        foreach (long index in new[] { long.MinValue, long.MinValue + 1, -26L, -25L, 0L, 89L, 90L, long.MaxValue - 1, long.MaxValue })
        {
            var expected = OriginalIchimokuSnapshot.Extended(bars, startIndex: index, endIndex: index).Single();
            var actual = IchimokuCloudSnapshot.Extended(bars, startIndex: index, endIndex: index).Single();
            Assert.Equal(expected.Index, actual.Index); Rows([expected.Value], [actual.Value]);
        }
    }
    [Fact]
    public void ExtendedColumnsCaptureAtCallAndCanBeEnumeratedAgain()
    {
        var bars = Data(129); var expected = OriginalIchimokuSnapshot.Extended(bars).ToArray();
        var result = IchimokuCloudSnapshot.Extended(bars); var regular = IchimokuCloudSnapshot.Calculate(bars);
        var regularExpected = OriginalIchimokuSnapshot.Calculate(bars);
        Array.Clear(bars);
        for (int replay = 0; replay < 2; replay++)
        {
            var actual = result.ToArray(); Assert.Equal(expected.Select(r => r.Index), actual.Select(r => r.Index));
            Rows(expected.Select(r => r.Value), actual.Select(r => r.Value));
        }
        Rows(regularExpected, regular);
    }
    [Fact]
    public void ExtremeMidpointsAndRawLaggingSignedZeroStayExact()
    {
        double[] values = [double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon, -0d, 0d];
        var bars = Data(129).Select((b, i) => new Bar(b.Time, double.NaN, values[i % values.Length], values[(i + 1) % values.Length], values[(i + 2) % values.Length], double.NaN)).ToArray();
        Rows(OriginalIchimokuSnapshot.Calculate(bars, 1, 2, 3, 0, 0), IchimokuCloudSnapshot.Calculate(bars, 1, 2, 3, 0, 0));
        Rows(OriginalIchimokuSnapshot.Extended(bars, 1, 2, 3).Select(r => r.Value), IchimokuCloudSnapshot.Extended(bars, 1, 2, 3).Select(r => r.Value));
    }
    [Fact]
    public void InvalidInputsFailAtCallEvenWithEmptyRequestedCoordinates()
    {
        var bars = Data(3); bars[^1] = new(default, 0, double.NaN, 0, 0, 0);
        var expected = Assert.Throws<ArgumentOutOfRangeException>(() => OriginalIchimokuSnapshot.Extended(bars, startIndex: 2, endIndex: 1));
        Assert.Equal(expected.Message, Assert.Throws<ArgumentOutOfRangeException>(() => IchimokuCloudSnapshot.Extended(bars, startIndex: 2, endIndex: 1)).Message);
        Assert.Equal("endIndex", Assert.Throws<ArgumentOutOfRangeException>(() => IchimokuCloudSnapshot.Extended([], startIndex: 2, endIndex: 1)).ParamName);
        Assert.Equal("forwardOffset", Assert.Throws<ArgumentOutOfRangeException>(() => IchimokuCloudSnapshot.Calculate(bars, forwardOffset: -1)).ParamName);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SnapshotAllocationsDropIncludingReturnedRows(bool extended)
    {
        var bars = Data(1000);
        Func<object> original = extended ? () => OriginalIchimokuSnapshot.Extended(bars).ToArray() : () => OriginalIchimokuSnapshot.Calculate(bars);
        Func<object> compact = extended ? () => IchimokuCloudSnapshot.Extended(bars).ToArray() : () => IchimokuCloudSnapshot.Calculate(bars);
        long Measure(Func<object> action) { long before = GC.GetAllocatedBytesForCurrentThread(); var result = action(); long used = GC.GetAllocatedBytesForCurrentThread() - before; GC.KeepAlive(result); return used; }
        Measure(original); Measure(compact); long oldBytes = Measure(original), newBytes = Measure(compact);
        log.WriteLine($"Ichimoku {(extended ? "Extended" : "Calculate")} 1000 bars including returned rows: original {oldBytes} bytes, compact {newBytes} bytes.");
        Assert.True(newBytes < oldBytes, $"Original {oldBytes}, compact {newBytes}");
    }
    private static void Rows(IEnumerable<IchimokuCloudValue> expected, IEnumerable<IchimokuCloudValue> actual)
    {
        static IEnumerable<double?> Fields(IchimokuCloudValue r) => new[] { r.Conversion, r.Base, r.LeadingA, r.LeadingB, r.Lagging };
        var e = expected.SelectMany(Fields).ToArray(); var a = actual.SelectMany(Fields).ToArray(); Assert.Equal(e.Length, a.Length);
        for (int i = 0; i < e.Length; i++) { Assert.Equal(e[i].HasValue, a[i].HasValue); if (e[i].HasValue) Assert.Equal(BitConverter.DoubleToInt64Bits(e[i]!.Value), BitConverter.DoubleToInt64Bits(a[i]!.Value)); }
    }
}

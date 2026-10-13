using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class PivotSnapshotSharedExecutionTests(Xunit.Abstractions.ITestOutputHelper log)
{
    private static Bar[] Data(int count, int shape = 0) => Enumerable.Range(0, count).Select(i =>
    {
        double value = shape switch
        {
            1 => i % 4 == 2 ? 5 : 1,
            2 => 1,
            3 => Math.ScaleB(Math.Sin(i * .73), 1023),
            4 => i % 4 == 2 ? double.Epsilon : -double.Epsilon,
            5 => i % 2 == 0 ? 0d : -0d,
            _ => 100 + Math.Sin(i * .71) * 10 + Math.Sin(i * .09)
        };
        return new Bar(DateTime.UnixEpoch.AddMinutes(i), value, value, -value, value, 1);
    }).ToArray();

    private static void Same(double? expected, double? actual)
    {
        Assert.Equal(expected.HasValue, actual.HasValue);
        if (expected.HasValue) Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Value), BitConverter.DoubleToInt64Bits(actual!.Value));
    }

    [Theory]
    [InlineData(2, 2, 20)] [InlineData(2, 3, 5)] [InlineData(3, 2, 4)]
    [InlineData(7, 9, 12)] [InlineData(int.MaxValue - 1, int.MaxValue, int.MaxValue)]
    public void EveryFieldMatchesOriginalAcrossRetrospectiveBoundaries(int left, int right, int maximum)
    {
        foreach (int count in new[] { 0, 1, 4, 5, 19, 129 })
        foreach (int shape in Enumerable.Range(0, 6))
        foreach (bool close in new[] { false, true })
        {
            var bars = Data(count, shape);
            var expected = OriginalPivotTrendSnapshot.Calculate(bars, left, right, maximum, close);
            var actual = PivotTrendSnapshot.Calculate(bars, left, right, maximum, close);
            Assert.Equal(expected.Count, actual.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Same(expected[i].HighPoint, actual[i].HighPoint); Same(expected[i].LowPoint, actual[i].LowPoint);
                Same(expected[i].HighLine, actual[i].HighLine); Same(expected[i].LowLine, actual[i].LowLine);
                Assert.Equal(expected[i].HighTrend, actual[i].HighTrend); Assert.Equal(expected[i].LowTrend, actual[i].LowTrend);
            }
        }
    }

    [Fact]
    public void ValidationPriorityAndOutputOwnershipRemainIntact()
    {
        Assert.Equal("maxTrendPeriods", Assert.Throws<ArgumentOutOfRangeException>(() => PivotTrendSnapshot.Calculate(null!, maxTrendPeriods: 2)).ParamName);
        Assert.Throws<ArgumentNullException>(() => PivotTrendSnapshot.Calculate(null!));
        var invalid = new[] { new Bar(DateTime.UnixEpoch, 0, double.NaN, 0, 0, 0) };
        Assert.Equal("bars", Assert.Throws<ArgumentOutOfRangeException>(() => PivotTrendSnapshot.Calculate(invalid, int.MaxValue - 1, int.MaxValue, int.MaxValue)).ParamName);
        Assert.Single(PivotTrendSnapshot.Calculate(invalid, useClose: true));
        var bars = Data(129); var rows = PivotTrendSnapshot.Calculate(bars); var saved = rows.ToArray();
        Array.Clear(bars); Assert.Equal(saved, rows);
    }

    [Fact]
    public void FinalRowsAvoidRepeatedRecordAllocation()
    {
        var bars = Data(1000);
        for (var i = 0; i < 3; i++) { OriginalPivotTrendSnapshot.Calculate(bars); PivotTrendSnapshot.Calculate(bars); }
        static long Measure(Func<IReadOnlyList<PivotTrendValue>> action)
        {
            var start = GC.GetAllocatedBytesForCurrentThread(); var rows = action();
            var size = GC.GetAllocatedBytesForCurrentThread() - start; GC.KeepAlive(rows); return size;
        }
        var original = Measure(() => OriginalPivotTrendSnapshot.Calculate(bars));
        var compact = Measure(() => PivotTrendSnapshot.Calculate(bars));
        log.WriteLine($"1000 bars including immutable outputs: original {original}, compact {compact} bytes.");
        Assert.True(compact < original, $"original={original}; compact={compact}");
    }
}

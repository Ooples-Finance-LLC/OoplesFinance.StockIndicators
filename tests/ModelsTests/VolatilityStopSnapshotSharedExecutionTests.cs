using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class VolatilityStopSnapshotSharedExecutionTests(Xunit.Abstractions.ITestOutputHelper log)
{
    private static Bar[] Data(int count, int shape = 0) => Enumerable.Range(0, count).Select(i =>
    {
        double value = shape switch
        {
            1 => 100 + i,
            2 => 100 - i,
            3 => double.MaxValue,
            4 => (i % 7 - 3) * double.Epsilon,
            5 => i % 2 == 0 ? 0d : -0d,
            _ => 100 + Math.Sin(i * .7) * 20
        };
        return new Bar(DateTime.UnixEpoch.AddMinutes(i), value, value, shape == 3 ? -value : value, value, 1);
    }).ToArray();

    private static void Same(double? expected, double? actual)
    {
        Assert.Equal(expected.HasValue, actual.HasValue);
        if (expected.HasValue) Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Value), BitConverter.DoubleToInt64Bits(actual!.Value));
    }

    [Theory]
    [InlineData(2, .5)] [InlineData(7, 3)] [InlineData(20, double.MaxValue)]
    [InlineData(int.MaxValue, 3)] [InlineData(3, double.Epsilon)]
    [InlineData(3, .1)] [InlineData(3, .75)] [InlineData(3, 1.0000000000000002)]
    public void FieldsReversalRemovalAndFailuresMatchOriginal(int period, double multiplier)
    {
        foreach (int count in new[] { 0, 1, 2, 3, 8, 21, 129 })
        foreach (int shape in Enumerable.Range(0, 6))
        {
            var bars = Data(count, shape);
            IReadOnlyList<VolatilityStopValue>? expected = null, actual = null;
            var before = Record.Exception(() => expected = OriginalVolatilityStopSnapshot.Calculate(bars, period, multiplier));
            var after = Record.Exception(() => actual = VolatilityStopSnapshot.Calculate(bars, period, multiplier));
            Assert.Equal(before?.GetType(), after?.GetType()); Assert.Equal(before?.Message, after?.Message);
            if (before is not null) continue;
            Assert.Equal(expected!.Count, actual!.Count);
            for (var i = 0; i < expected.Count; i++)
            {
                Same(expected[i].Sar, actual[i].Sar); Same(expected[i].UpperBand, actual[i].UpperBand);
                Same(expected[i].LowerBand, actual[i].LowerBand); Assert.Equal(expected[i].IsStop, actual[i].IsStop);
            }
        }
    }

    [Fact]
    public void ValidationPriorityAndOwnedResultsRemainIntact()
    {
        Assert.Throws<ArgumentNullException>(() => VolatilityStopSnapshot.Calculate(null!, 1));
        var bad = Data(10, 3); bad[^1] = new(DateTime.UnixEpoch, 0, double.NaN, 0, 0, 0);
        Assert.Equal("period", Assert.Throws<ArgumentOutOfRangeException>(() => VolatilityStopSnapshot.Calculate(bad, 1)).ParamName);
        Assert.Equal("bars", Assert.Throws<ArgumentOutOfRangeException>(() => VolatilityStopSnapshot.Calculate(bad, 2, 2)).ParamName);
        var bars = Data(129); var rows = VolatilityStopSnapshot.Calculate(bars); var saved = rows.ToArray();
        Array.Clear(bars); Assert.Equal(saved, rows);
    }

    [Fact]
    public void InvariantOperandsReduceAllocationIncludingResults()
    {
        var bars = Data(1000);
        for (int i = 0; i < 3; i++) { OriginalVolatilityStopSnapshot.Calculate(bars); VolatilityStopSnapshot.Calculate(bars); }
        static long Measure(Func<IReadOnlyList<VolatilityStopValue>> action)
        {
            var start = GC.GetAllocatedBytesForCurrentThread(); var rows = action();
            var size = GC.GetAllocatedBytesForCurrentThread() - start; GC.KeepAlive(rows); return size;
        }
        var original = Measure(() => OriginalVolatilityStopSnapshot.Calculate(bars));
        var compact = Measure(() => VolatilityStopSnapshot.Calculate(bars));
        log.WriteLine($"1000bars including results: original {original}, compact {compact} bytes.");
        Assert.True(compact < original, $"original={original}; compact={compact}");
    }
}

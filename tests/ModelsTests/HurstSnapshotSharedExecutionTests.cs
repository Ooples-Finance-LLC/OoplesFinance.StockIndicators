using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class HurstSnapshotSharedExecutionTests(Xunit.Abstractions.ITestOutputHelper log)
{
    private static Bar[] Data(int count) => Enumerable.Range(0, count)
        .Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, 0, 0, 100 + Math.Sin(i * .71) * 10 + i % 13, 0)).ToArray();

    [Theory]
    [InlineData(20)] [InlineData(21)] [InlineData(100)]
    [InlineData(340)] [InlineData(341)] [InlineData(int.MaxValue)]
    public void AllNullableOutputsRemainBitwiseIdentical(int period)
    {
        foreach (var count in new[] { 0, 1, 20, 21, 101, 401 })
        {
            var bars = Data(count);
            Check(bars, period);
            double[] extremes = [double.Epsilon, double.MaxValue, 1, -0d, 0d, -1];
            Check(bars.Select((b, i) => new Bar(b.Time, 0, 0, 0, extremes[i % extremes.Length], 0)).ToArray(), period);
            Check(bars.Select(b => new Bar(b.Time, 0, 0, 0, 1, 0)).ToArray(), period);
        }
    }

    private static void Same(double? expected, double? actual)
    {
        Assert.Equal(expected.HasValue, actual.HasValue);
        if (expected.HasValue) Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Value), BitConverter.DoubleToInt64Bits(actual!.Value));
    }

    private static void Check(Bar[] bars, int period)
    {
        var expected = OriginalHurstSnapshot.Calculate(bars, period);
        var actual = HurstSnapshot.Calculate(bars, period);
        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Same(expected[i].HurstExponent, actual[i].HurstExponent);
            Same(expected[i].HurstExponentAL, actual[i].HurstExponentAL);
        }
    }

    [Fact]
    public void ValidationStillPrecedesShortHistoryAndOutputsOwnTheirValues()
    {
        Assert.Throws<ArgumentNullException>(() => HurstSnapshot.Calculate(null!, 19));
        var bad = new[] { new Bar(DateTime.UnixEpoch, 0, 0, 0, double.NaN, 0) };
        Assert.Equal("period", Assert.Throws<ArgumentOutOfRangeException>(() => HurstSnapshot.Calculate(bad, 19)).ParamName);
        Assert.Equal("bars", Assert.Throws<ArgumentOutOfRangeException>(() => HurstSnapshot.Calculate(bad, int.MaxValue)).ParamName);
        var bars = Data(101);
        var result = HurstSnapshot.Calculate(bars, 20);
        var saved = result.ToArray();
        Array.Clear(bars);
        Assert.Equal(saved, result);
        Assert.All(result, row => Assert.NotNull(row));
    }

    [Fact]
    public void ScratchReuseReducesAllocationIncludingReturnedRows()
    {
        var bars = Data(1000);
        for (int i = 0; i < 3; i++)
        {
            OriginalHurstSnapshot.Calculate(bars, 100);
            HurstSnapshot.Calculate(bars, 100);
        }
        static long Measure(Func<IReadOnlyList<HurstValue>> calculate)
        {
            long start = GC.GetAllocatedBytesForCurrentThread();
            var result = calculate();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - start;
            GC.KeepAlive(result);
            return allocated;
        }
        var original = Measure(() => OriginalHurstSnapshot.Calculate(bars, 100));
        var compact = Measure(() => HurstSnapshot.Calculate(bars, 100));
        log.WriteLine($"1000 bars, period100, including outputs: original {original}, compact {compact} bytes.");
        Assert.True(compact < original * .6, $"original={original}; compact={compact}");
    }
}

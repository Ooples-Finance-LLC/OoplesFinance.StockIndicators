using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class ParabolicSnapshotSharedExecutionTests(Xunit.Abstractions.ITestOutputHelper log)
{
    private static Bar[] Data(int count, int shape = 0) => Enumerable.Range(0, count).Select(i =>
    {
        double middle = shape switch { 1 => 100 + i, 2 => 100 - i, 3 => 1, _ => 100 + Math.Sin(i * .7) * 10 };
        double high = middle + 2, low = middle - 2;
        if (shape == 4) { high = double.MaxValue; low = -double.MaxValue; }
        if (shape == 5) { high = double.Epsilon; low = -double.Epsilon; }
        if (shape == 6) { high = 0d; low = -0d; }
        return new Bar(DateTime.UnixEpoch.AddMinutes(i), middle, high, low, middle, 1);
    }).ToArray();

    private static double?[][] Run(Bar[] bars, int kind, bool original, bool custom = false)
    {
        if (kind == 2)
        {
            var rows = original
                ? OriginalParabolicStopSnapshots.Confirmed(bars, custom ? .04 : .02, .2, custom ? .03 : null)
                : ParabolicStopSnapshots.Confirmed(bars, custom ? .04 : .02, .2, custom ? .03 : null);
            return [rows.Select(r => r.Sar).ToArray(), rows.Select(r => r.IsReversal.HasValue ? (double?)(r.IsReversal.Value ? 1 : 0) : null).ToArray()];
        }
        var values = (kind, original) switch
        {
            (0, true) => OriginalParabolicStopSnapshots.Classic(bars, custom ? 0 : .02),
            (0, false) => ParabolicStopSnapshots.Classic(bars, custom ? 0 : .02),
            (1, true) => OriginalParabolicStopSnapshots.Extended(bars, custom ? -105 : 0, custom ? .01 : 0, .03, .04, .2, .02, .06, .3),
            (1, false) => ParabolicStopSnapshots.Extended(bars, custom ? -105 : 0, custom ? .01 : 0, .03, .04, .2, .02, .06, .3),
            (3, true) => OriginalParabolicStopSnapshots.FourBar(bars, custom ? -.01 : .02),
            (3, false) => ParabolicStopSnapshots.FourBar(bars, custom ? -.01 : .02),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        return [values.ToArray()];
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void StopsReversalsAndFailuresMatchOriginalBitwise(int kind)
    {
        foreach (var count in new[] { 0, 1, 2, 4, 5, 6, 33, 129 })
        foreach (var shape in Enumerable.Range(0, 7))
        foreach (var custom in new[] { false, true })
        {
            var bars = Data(count, shape);
            double?[][]? expected = null, actual = null;
            var expectedError = Record.Exception(() => expected = Run(bars, kind, true, custom));
            var actualError = Record.Exception(() => actual = Run(bars, kind, false, custom));
            Assert.Equal(expectedError?.GetType(), actualError?.GetType());
            Assert.Equal(expectedError?.Message, actualError?.Message);
            if (expectedError is not null) continue;
            Assert.Equal(expected!.Length, actual!.Length);
            for (var column = 0; column < expected.Length; column++)
            {
                Assert.Equal(expected[column].Length, actual[column].Length);
                for (var i = 0; i < expected[column].Length; i++)
                {
                    Assert.Equal(expected[column][i].HasValue, actual[column][i].HasValue);
                    if (expected[column][i].HasValue)
                        Assert.Equal(BitConverter.DoubleToInt64Bits(expected[column][i]!.Value), BitConverter.DoubleToInt64Bits(actual[column][i]!.Value));
                }
            }
        }
    }

    [Fact]
    public void LateInvalidInputStillWinsOverEarlierPublicationOverflow()
    {
        var bars = Data(8, 4);
        bars[^1] = new Bar(DateTime.UnixEpoch, 0, double.NaN, 0, 0, 0);
        Assert.Equal("bars", Assert.Throws<ArgumentOutOfRangeException>(() => ParabolicStopSnapshots.Extended(bars, offsetOnReverse: 1)).ParamName);
        var source = Data(80);
        var rows = ParabolicStopSnapshots.Confirmed(source);
        var saved = rows.ToArray();
        Array.Clear(source);
        Assert.Equal(saved, rows);
        Assert.All(rows, row => Assert.NotNull(row));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void CachedExactConversionsReduceAllocation(int kind)
    {
        var bars = Data(1000);
        for (var i = 0; i < 3; i++) { Run(bars, kind, true); Run(bars, kind, false); }
        long Measure(bool original)
        {
            var start = GC.GetAllocatedBytesForCurrentThread();
            var rows = Run(bars, kind, original);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - start;
            GC.KeepAlive(rows);
            return allocated;
        }
        var before = Measure(true); var after = Measure(false);
        log.WriteLine($"kind{kind},1000bars incl outputs/comparison columns: original {before}, compact {after} bytes.");
        Assert.True(after < before, $"kind{kind}: original={before}, compact={after}");
    }
}

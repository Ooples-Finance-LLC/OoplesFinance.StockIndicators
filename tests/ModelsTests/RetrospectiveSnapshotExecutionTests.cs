using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class RetrospectiveSnapshotExecutionTests(Xunit.Abstractions.ITestOutputHelper log)
{
    private static Bar[] Data(int count) => Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 100, 110 + i % 17, 90 - i % 13, 100 + i % 7, i % 13)).ToArray();
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(20)] [InlineData(4097)] [InlineData(int.MaxValue)]
    public void DpoMatchesOriginalAtStartupTrailingAlignmentAndWindowWrap(int period)
    {
        foreach (int count in new[] { 0, 1, 5, 39, 41, 513, 10000 })
        {
            var input = Data(count); var expected = OriginalDpo(input, period);
            var actual = DetrendedPriceSnapshot.Calculate(input, period);
            Array.Clear(input);
            Assert.Equal(expected.Count, actual.Count);
            for (int i = 0; i < actual.Count; i++) { Bits(expected[i].Dpo, actual[i].Dpo); Bits(expected[i].Sma, actual[i].Sma); }
        }
    }
    [Theory]
    [InlineData(2)] [InlineData(20)] [InlineData(int.MaxValue)]
    public void DirectChaosConfirmationMatchesMaterializedFractals(int span)
    {
        foreach (int count in new[] { 0, 1, 5, 39, 41, 513, 10000 })
        {
            var input = Data(count); var expected = OriginalChaos(input, span);
            var actual = FractalSnapshot.ChaosBands(input, span);
            Array.Clear(input);
            Assert.Equal(expected.Count, actual.Count);
            for (int i = 0; i < actual.Count; i++) { Bits(expected[i].Upper, actual[i].Upper); Bits(expected[i].Lower, actual[i].Lower); }
        }
    }
    [Fact]
    public void ExtremeCancellationSubnormalAndSignedZeroDpoRetainExactRounding()
    {
        foreach (double scale in new[] { double.Epsilon, 1e-200, -1d, Math.ScaleB(1d, 900) })
        {
            var bars = Data(129).Select(b => new Bar(b.Time, b.Open, b.High, b.Low, b.Close * scale, b.Volume)).ToArray();
            foreach (int period in new[] { 1, 2, 3, 20 })
            {
                var expected = OriginalDpo(bars, period); var actual = DetrendedPriceSnapshot.Calculate(bars, period);
                for (int i = 0; i < bars.Length; i++) { Bits(expected[i].Dpo, actual[i].Dpo); Bits(expected[i].Sma, actual[i].Sma); }
            }
        }
        foreach (double price in new[] { -0d, double.MaxValue, -double.MaxValue })
        {
            var bars = Enumerable.Repeat(new Bar(default, 0, 0, 0, price, 0), 65).ToArray();
            var expected = OriginalDpo(bars, 20); var actual = DetrendedPriceSnapshot.Calculate(bars, 20);
            for (int i = 0; i < bars.Length; i++) { Bits(expected[i].Dpo, actual[i].Dpo); Bits(expected[i].Sma, actual[i].Sma); }
        }
        var overflow = new[] { new Bar(default, 0, 0, 0, double.MaxValue, 0), new Bar(default, 0, 0, 0, -double.MaxValue, 0) };
        var original = Assert.Throws<OverflowException>(() => OriginalDpo(overflow, 1));
        Assert.Equal(original.Message, Assert.Throws<OverflowException>(() => DetrendedPriceSnapshot.Calculate(overflow, 1)).Message);
    }
    [Fact]
    public void ValidationNamesSelectedFieldsAndRawPriorityArePreserved()
    {
        Assert.Equal("bars", Assert.Throws<ArgumentNullException>(() => FractalSnapshot.ChaosBands(null!)).ParamName);
        Assert.Equal("leftSpan", Assert.Throws<ArgumentOutOfRangeException>(() => FractalSnapshot.ChaosBands([], 1)).ParamName);
        var bars = Data(65); bars[0] = new(default, 0, 0, 0, double.MaxValue, 0); bars[1] = new(default, 0, 0, 0, -double.MaxValue, 0);
        bars[^1] = new(default, 0, 0, 0, double.NaN, 0);
        Assert.Equal("bars", Assert.Throws<ArgumentOutOfRangeException>(() => DetrendedPriceSnapshot.Calculate(bars, 1)).ParamName);
        bars = Data(65).Select(b => new Bar(b.Time, double.NaN, b.High, b.Low, b.Close, double.NaN)).ToArray();
        Assert.Equal(OriginalDpo(bars, 20), DetrendedPriceSnapshot.Calculate(bars, 20));
        Assert.Equal(OriginalChaos(bars, 2), FractalSnapshot.ChaosBands(bars, 2));
        bars[^1] = new(default, 0, double.NaN, 0, 0, 0);
        Assert.Equal("bars", Assert.Throws<ArgumentOutOfRangeException>(() => FractalSnapshot.ChaosBands(bars, int.MaxValue)).ParamName);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SnapshotAllocationDropsWithoutDiscardingRecords(bool dpo)
    {
        var bars = Data(1000);
        Func<object> original = dpo ? () => OriginalDpo(bars, 20) : () => OriginalChaos(bars, 20);
        Func<object> compact = dpo ? () => DetrendedPriceSnapshot.Calculate(bars, 20) : () => FractalSnapshot.ChaosBands(bars, 20);
        long Measure(Func<object> action)
        {
            long before = GC.GetAllocatedBytesForCurrentThread(); var result = action();
            long used = GC.GetAllocatedBytesForCurrentThread() - before; GC.KeepAlive(result); return used;
        }
        Measure(original); Measure(compact);
        long oldBytes = Measure(original), newBytes = Measure(compact);
        log.WriteLine($"Snapshot {(dpo ? "Dpo" : "Chaos")} 1000 bars including returned records: original {oldBytes} bytes, compact {newBytes} bytes.");
        Assert.True(newBytes < oldBytes, $"Original {oldBytes}, compact {newBytes}");
    }
    private static void Bits(double? expected, double? actual)
    {
        Assert.Equal(expected.HasValue, actual.HasValue);
        if (expected.HasValue) Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Value), BitConverter.DoubleToInt64Bits(actual!.Value));
    }
    private static IReadOnlyList<FractalChaosValue> OriginalChaos(Bar[] bars, int span)
    {
        var fractals = FractalSnapshot.Calculate(bars, span, span); var result = new FractalChaosValue[bars.Length];
        double? upper = null, lower = null;
        for (int i = 0; i < bars.Length; i++)
        {
            if ((long)i >= 2L * span) { upper = fractals[i - span].Bear ?? upper; lower = fractals[i - span].Bull ?? lower; }
            result[i] = new(upper, lower);
        }
        return result;
    }
    private static IReadOnlyList<DetrendedPriceValue> OriginalDpo(Bar[] bars, int period)
    {
        var result = Enumerable.Range(0, bars.Length).Select(_ => new DetrendedPriceValue(null, null)).ToArray();
        int offset = period / 2 + 1; var sum = new ExactMeanAccumulator();
        for (int end = 0; end < bars.Length; end++)
        {
            sum.Add(bars[end].Close); if (end >= period) sum.Add(bars[end - period].Close, -1);
            if (end < period - 1 || end < offset) continue;
            double mean = sum.Mean(period); var difference = new ExactMeanAccumulator();
            difference.Add(bars[end - offset].Close); difference.Add(mean, -1); double value = difference.Mean(1);
            if (!double.IsFinite(value)) throw new OverflowException("Detrended price is not representable.");
            result[end - offset] = new(value, mean);
        }
        return result;
    }
}

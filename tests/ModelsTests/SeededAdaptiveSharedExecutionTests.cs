using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class SeededAdaptiveSharedExecutionTests(Xunit.Abstractions.ITestOutputHelper log)
{
    private static Bar[] BarsOf(IEnumerable<double> prices) => prices.Select((p, i) =>
        new Bar(DateTime.UnixEpoch.AddMinutes(i), i, i + 1, i - 1, p, i + 2)).ToArray();

    [Theory]
    [InlineData(false, false, true, false)] [InlineData(true, true, false, false)]
    [InlineData(false, false, false, true)] [InlineData(true, false, true, true)]
    [InlineData(true, true, false, true)] [InlineData(false, true, false, false)]
    public async Task SeedWindowExpirationAndAllOwnedColumnsMatchOriginal(bool publish, bool clamp, bool reset, bool fastFlat)
    {
        foreach (int period in new[] { 1, 3, 20, int.MaxValue })
        foreach (int count in new[] { 0, 1, 19, 20, 21, 65, 513 })
        {
            var bars = BarsOf(Enumerable.Range(0, count).Select(i => i % 97 < 45 ? 100 : 100 + (i * 13 % 101) / 8d));
            await Check(bars, new(period, 2, 30, publish, clamp, reset, fastFlat));
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ExtremeDifferencesSubnormalRoundingAndReversedRatesRemainFinite(bool fastFlat)
    {
        foreach (var rates in new[] { (1, int.MaxValue), (int.MaxValue, 1), (2, 30), (30, 2) })
        {
            var indicator = new SeededAdaptiveAverage(3, rates.Item1, rates.Item2, true, false, false, fastFlat);
            foreach (int exponent in new[] { -1074, -1022, -500, 0, 900 })
                await Check(BarsOf(Enumerable.Range(0, 33).Select(i => Math.ScaleB(i % 13 - 6d, exponent))), indicator);
            double[] values = [double.MaxValue, -double.MaxValue, double.MaxValue / 2, -double.MaxValue / 2,
                double.Epsilon, -double.Epsilon, 0d, -0d];
            await Check(BarsOf(Enumerable.Range(0, 65).Select(i => values[i % values.Length])), indicator);
            foreach (double price in new[] { -0d, double.Epsilon, double.MaxValue, -double.MaxValue })
                await Check(BarsOf(Enumerable.Repeat(price, 17)), indicator);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task PublicationDelayDoesNotSuppressEfficiency(bool publish)
    {
        var bars = BarsOf(Enumerable.Range(0, 33).Select(i => i % 7d));
        foreach (long delay in new[] { 1L, 7L, long.MaxValue - (publish ? 0 : 2) })
            await Check(bars, new SeededAdaptiveAverage(3, 2, 30, publish, false, true, false, delay));
    }

    [Fact]
    public void StateResetAndRandomExponentSpansMatchOriginal()
    {
        var random = new Random(78111);
        var bars = BarsOf(Enumerable.Range(0, 129).Select(i =>
            Math.ScaleB(random.NextDouble() * 2 - 1, random.Next(-1074, 1024))));
        foreach (bool reset in new[] { false, true })
        {
            var indicator = new SeededAdaptiveAverage(7, 1, int.MaxValue, true, false, reset, true);
            var original = (IMultiOutputState)indicator.CreateState();
            var compact = new ValuesBarExecution.SeededAdaptiveValueState(indicator, bars.Length);
            var expected = new double[4]; var actual = new double[4];
            for (int replay = 0; replay < 2; replay++)
            {
                original.Reset(); compact.Reset();
                foreach (var bar in bars)
                {
                    original.Update(bar, expected); compact.Update(bar, actual);
                    AssertBits(expected, actual);
                }
            }
        }
    }

    [Fact]
    public void OrdinaryWindowsAvoidPaddedPerBarAllocations()
    {
        var bars = BarsOf(Enumerable.Range(0, 256).Select(i => 100 + (i * 13 % 101) / 8d));
        var indicator = new SeededAdaptiveAverage(20);
        var original = (IMultiOutputState)indicator.CreateState();
        var compact = new ValuesBarExecution.SeededAdaptiveValueState(indicator, bars.Length);
        var output = new double[4];
        long Measure(IMultiOutputState state)
        {
            state.Reset();
            long before = GC.GetAllocatedBytesForCurrentThread();
            foreach (var bar in bars) state.Update(bar, output);
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
        Measure(original); Measure(compact);
        long oldBytes = Measure(original), newBytes = Measure(compact);
        log.WriteLine($"256 updates: original {oldBytes} bytes, compact {newBytes} bytes.");
        Assert.True(newBytes < oldBytes / 4, $"Compact {newBytes}, original {oldBytes}");
    }

    [Fact]
    public async Task MixedDuplicateRootsRetainIndependentState()
    {
        var bars = BarsOf(Enumerable.Range(0, 65).Select(i => 100 + i % 7d));
        var first = new SeededAdaptiveAverage(3);
        var second = new SeededAdaptiveAverage(7, 1, 30, true, true, false, true);
        var engine = new CustomIndicatorEngine(bars, _ => null);
        var a = engine.Compute(first); var b = engine.Compute(second);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(first, second, first).ConfigureHistory(history).BuildAsync();
            for (int slot = 0; slot < 4; slot++)
            {
                AssertBits(a[slot], run[first.Outputs[slot]].ToArray());
                AssertBits(b[slot], run[second.Outputs[slot]].ToArray());
            }
        }
    }

    [Fact]
    public async Task ValidationCancellationAndBackendContractsRecover()
    {
        var bars = BarsOf(Enumerable.Repeat(1d, 33));
        var indicator = new SeededAdaptiveAverage(3);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator);
        bars[^1] = new(default, 1, 1, 1, 1, double.NaN);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
        bars[^1] = new(default, 1, 1, 1, 1, 1);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        using var run = await builder.ConfigureExecution(IndicatorExecutionBackend.Auto).BuildAsync();
        Assert.Equal(IndicatorExecutionBackend.Cpu, builder.LastExecution!.Backend);
        Assert.Equal(1, run[indicator.Average].ToArray()[^1]);
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
    }

    private static async Task Check(Bar[] bars, SeededAdaptiveAverage indicator)
    {
        var state = (IMultiOutputState)indicator.CreateState();
        var expected = Enumerable.Range(0, 4).Select(_ => new double[bars.Length]).ToArray();
        var scratch = new double[4];
        for (int i = 0; i < bars.Length; i++)
        {
            state.Update(bars[i], scratch);
            for (int slot = 0; slot < 4; slot++) expected[slot][i] = scratch[slot];
        }
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var input = bars.ToArray();
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input))
                .ConfigureIndicators(indicator).ConfigureHistory(mode).BuildAsync();
            Array.Clear(input); run.Dispose();
            for (int slot = 0; slot < 4; slot++) AssertBits(expected[slot], run[indicator.Outputs[slot]].ToArray());
            if (bars.Length > 0) Assert.Equal(bars[^1], run.Latest.Bar);
            if (mode == IndicatorHistoryMode.Full)
            {
                int row = 0;
                await foreach (var snapshot in run) Assert.Equal(bars[row++], snapshot.Bar);
                Assert.Equal(bars.Length, row);
            }
        }
    }
    private static void AssertBits(double[] expected, double[] actual) =>
        Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
}

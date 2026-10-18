using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class QuarticSharedExecutionTests(Xunit.Abstractions.ITestOutputHelper log)
{
    private static Bar[] BarsOf(IEnumerable<double> prices) => prices.Select((p, i) =>
        new Bar(DateTime.UnixEpoch.AddMinutes(i), i, i + 1, i - 1, p, i + 2)).ToArray();

    [Theory]
    [InlineData(1, .6)] [InlineData(3, 2d)] [InlineData(20, .6)] [InlineData(int.MaxValue, 16d)]
    public async Task BothStartupModesAndOwnedHistoriesMatchOriginal(int period, double factor)
    {
        foreach (var startup in Enum.GetValues<McGinleyStartup>())
        foreach (int count in new[] { 0, 1, 2, 33, 513 })
            await Check(BarsOf(Enumerable.Range(0, count).Select(i => 100 + (i * 13 % 101) / 8d)), period, factor, startup);
    }

    [Theory]
    [InlineData(McGinleyStartup.Immediate)] [InlineData(McGinleyStartup.ResetDelay)]
    public async Task RestartSingularityCancellationAndExtremeScalesMatchOriginal(McGinleyStartup startup)
    {
        foreach (double[] prices in new double[][] {
            [0d, -0d, 0, 1, 1, 1, 1, 1], [1d, -1, 0, 1, 2, 3],
            [1d, 0, 2], [-1d, 0, -2], [-double.MaxValue, double.MaxValue],
            [double.MaxValue, -double.MaxValue], [double.Epsilon, -double.Epsilon, double.Epsilon] })
        foreach (double factor in new[] { .6, 2, 16, double.Epsilon, double.MaxValue })
            await Check(BarsOf(prices), 3, factor, startup);
        foreach (int exponent in new[] { -1074, -1022, -500, 0, 900 })
            await Check(BarsOf(Enumerable.Range(0, 33).Select(i => Math.ScaleB(100 + (i * 13 % 23), exponent))),
                20, .6, startup);
        await Check(BarsOf([0d, -double.Epsilon, -double.Epsilon]), int.MaxValue, double.MaxValue, startup);
        await Check(BarsOf([1d, -1, 1, 1]), 1, 2, startup); // Exact cancellation to zero and restart.
    }

    [Theory]
    [InlineData(McGinleyStartup.Immediate)] [InlineData(McGinleyStartup.ResetDelay)]
    public void CompactStateResetAndRandomExponentSpansMatchOriginal(McGinleyStartup startup)
    {
        var random = new Random(91831);
        foreach (double factor in new[] { .6, double.Epsilon, double.MaxValue })
        {
            var indicator = new QuarticDynamicAverage(7, factor, startup);
            var original = (IMultiOutputState)indicator.CreateState();
            var compact = new ValuesBarExecution.QuarticValueState(7, factor, startup);
            var bars = BarsOf(Enumerable.Range(0, 96).Select(i =>
                Math.ScaleB((random.NextDouble() * 2 - 1), random.Next(-1074, 1024))));
            var expected = new double[2]; var actual = new double[2];
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
    public void OrdinaryRecurrenceAvoidsPaddedFourthPowerAllocations()
    {
        var bars = BarsOf(Enumerable.Range(0, 256).Select(i => 100 + (i * 13 % 101) / 8d));
        var original = (IMultiOutputState)new QuarticDynamicAverage(20).CreateState();
        var compact = new ValuesBarExecution.QuarticValueState(20, .6, McGinleyStartup.Immediate);
        var output = new double[2];
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
        Assert.True(newBytes < oldBytes / 2, $"Compact {newBytes}, original {oldBytes}");
    }

    [Fact]
    public async Task MixedDuplicateRootsRetainIndependentState()
    {
        var bars = BarsOf(Enumerable.Range(0, 65).Select(i => 100 + i % 7d));
        var first = new QuarticDynamicAverage(3);
        var second = new QuarticDynamicAverage(7, 2, McGinleyStartup.ResetDelay);
        var engine = new CustomIndicatorEngine(bars, _ => null);
        var a = engine.Compute(first); var b = engine.Compute(second);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(first, second, first).ConfigureHistory(history).BuildAsync();
            for (int slot = 0; slot < 2; slot++)
            {
                AssertBits(a[slot], run[first.Outputs[slot]].ToArray());
                AssertBits(b[slot], run[second.Outputs[slot]].ToArray());
            }
        }
    }

    [Fact]
    public async Task InputValidationPrecedesOutputFailureAndCancellationAndBackendRecover()
    {
        var bars = BarsOf([1d, 0, 1]); // A singular output precedes an invalid raw input.
        bars[^1] = new(default, 1, 1, 1, 1, double.NaN);
        var indicator = new QuarticDynamicAverage();
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
        bars[1] = new(default, 1, 1, 1, 1, 1);
        bars[^1] = new(default, 1, 1, 1, 1, 1);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        using var run = await builder.ConfigureExecution(IndicatorExecutionBackend.Auto).BuildAsync();
        Assert.Equal(IndicatorExecutionBackend.Cpu, builder.LastExecution!.Backend);
        Assert.All(run[indicator.Value].ToArray(), v => Assert.Equal(1, v));
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
    }

    private static async Task Check(Bar[] bars, int period, double factor, McGinleyStartup startup)
    {
        var indicator = new QuarticDynamicAverage(period, factor, startup);
        var state = (IMultiOutputState)indicator.CreateState();
        var expected = new[] { new double[bars.Length], new double[bars.Length] };
        var scratch = new double[2];
        for (int i = 0; i < bars.Length; i++)
        {
            state.Update(bars[i], scratch);
            for (int slot = 0; slot < 2; slot++) expected[slot][i] = scratch[slot];
        }
        int failure = Array.FindIndex(expected[0], v => !double.IsFinite(v));
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var input = bars.ToArray();
            var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(input))
                .ConfigureIndicators(indicator).ConfigureHistory(mode);
            if (failure >= 0)
            {
                var exception = await Assert.ThrowsAsync<IndicatorOutputException>(() => builder.BuildAsync());
                Assert.Equal(0, exception.OutputSlot);
                Assert.Equal(failure, exception.BarIndex);
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected[0][failure]), BitConverter.DoubleToInt64Bits(exception.Value));
                continue;
            }
            using var run = await builder.BuildAsync();
            Array.Clear(input); run.Dispose();
            for (int slot = 0; slot < 2; slot++) AssertBits(expected[slot], run[indicator.Outputs[slot]].ToArray());
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

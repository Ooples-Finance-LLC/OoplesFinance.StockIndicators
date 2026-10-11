using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class TensorsWeightedAverageGpuExecutionTests
{
    [SkippableTheory]
    [InlineData(-1)] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(20)] [InlineData(65)]
    [InlineData(4096)] [InlineData(65535)]
    public async Task CertifiedDeviceWeightsMatchExactCpuAcrossBlockBoundaries(int period)
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        foreach (int count in new[] { 1, 63, 64, 65, 513, 8193 })
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var bars = Enumerable.Range(0, count).Select(i => new Bar(default, 0, 1, -1,
                i % 7 == 0 ? -0d : (i % 31 - 15) / 8d, 1)).ToArray();
            if (period <= 1)
            {
                bars[0] = new Bar(default, 0, 1, -1, double.Epsilon, 1);
                if (count > 1) bars[^1] = new Bar(default, 0, 1, -1, double.MaxValue, 1);
            }
            var sum = new Wma(period);
            using var cpu = await Builder(bars, sum, history).ConfigureExecution(IndicatorExecutionBackend.Cpu).ConfigureBehavior(_ => { }).BuildAsync();
            var builder = Builder(bars, sum, history); using var gpu = await builder.BuildAsync();
            Assert.Equal(IndicatorExecutionBackend.Gpu, builder.LastExecution!.Backend);
            Assert.Equal(cpu[sum].ToArray().Select(BitConverter.DoubleToInt64Bits), gpu[sum].ToArray().Select(BitConverter.DoubleToInt64Bits));
            if (history == IndicatorHistoryMode.Full)
            {
                int index = 0; await foreach (var snapshot in gpu) Assert.Equal(bars[index++], snapshot.Bar);
                Assert.Equal(count, index);
            }
        }
    }

    [SkippableFact]
    public async Task RejectionInputOrderingRecoveryAndConcurrentWorkspaces()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var bars = Enumerable.Range(0, 513).Select(i => new Bar(default, 0, 1, -1, i % 31 / 8d, 1)).ToArray();
        var sum = new Wma(20); var builder = Builder(bars, sum); var original = bars.ToArray();
        using var saved = await builder.BuildAsync();
        foreach (double unsupported in new[] { double.Epsilon, double.MaxValue, .1 })
        {
            bars[0] = new Bar(default, 0, 1, -1, unsupported, 1);
            await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync());
            Assert.Null(builder.LastExecution);
        }
        bars[^1] = new Bar(default, 0, 1, -1, 0, double.NaN);
        var expected = await Record.ExceptionAsync(() => Builder(bars, sum).ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync());
        var actual = await Record.ExceptionAsync(() => builder.BuildAsync());
        Assert.NotNull(expected); Assert.NotNull(actual);
        Assert.Equal(expected.GetType(), actual.GetType()); Assert.Equal(expected.Message, actual.Message);
        foreach (int period in new[] { 65536, int.MaxValue })
        {
            var large = new Wma(period);
            var invalid = await Record.ExceptionAsync(() => Builder(bars, large).BuildAsync());
            Assert.NotNull(invalid); Assert.Equal(expected.GetType(), invalid.GetType()); Assert.Equal(expected.Message, invalid.Message);
            original.CopyTo(bars, 0);
            await Assert.ThrowsAsync<NotSupportedException>(() => Builder(bars, large).BuildAsync());
            bars[^1] = new Bar(default, 0, 1, -1, 0, double.NaN);
        }
        original.CopyTo(bars, 0); using var recovered = await builder.BuildAsync();
        Assert.Equal(saved[sum].ToArray(), recovered[sum].ToArray());
        await Task.WhenAll(Enumerable.Range(1, 4).Select(period => Task.Run(async () =>
        {
            var other = new Wma(period);
            using var cpu = await Builder(bars, other).ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
            using var gpu = await Builder(bars, other).BuildAsync();
            Assert.Equal(cpu[other].ToArray(), gpu[other].ToArray());
        })));
        Array.Clear(bars); int index = 0;
        await foreach (var snapshot in saved) Assert.Equal(original[index++], snapshot.Bar);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancel.Token));
    }
    private static StockIndicatorBuilder Builder(Bar[] bars, IIndicator sum, IndicatorHistoryMode history = IndicatorHistoryMode.Full) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(sum).ConfigureHistory(history)
            .ConfigureExecution(IndicatorExecutionBackend.Gpu);
}

using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class TensorsEndpointWeightedGpuExecutionTests
{
    [SkippableTheory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(20)]
    [InlineData(65)] [InlineData(4096)] [InlineData(32767)]
    public async Task SignedDeviceRecurrenceMatchesExactCpuAcrossBlocks(int period)
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        foreach (int count in new[] { 1, 63, 64, 65, 513 })
        foreach (bool mean in new[] { false, true })
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var bars = Enumerable.Range(0, count).Select(i => new Bar(default, 0, 1, -1,
                i % 7 == 0 ? -0d : (i % 31 - 15) / 8d, 1)).ToArray();
            if (period == 1) bars[0] = new Bar(default, 0, 1, -1, double.Epsilon, 1);
            var indicator = new EndpointWeightedAverage(period, mean);
            using var cpu = await Build(bars, indicator, history).ConfigureExecution(IndicatorExecutionBackend.Cpu)
                .ConfigureBehavior(_ => { }).BuildAsync();
            var builder = Build(bars, indicator, history);
            using var gpu = await builder.BuildAsync(); gpu.Dispose();
            Assert.Equal(IndicatorExecutionBackend.Gpu, builder.LastExecution!.Backend);
            Assert.Equal(cpu[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits), gpu[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits));
            if (history == IndicatorHistoryMode.Full)
            {
                int index = 0; await foreach (var snapshot in gpu) Assert.Equal(bars[index++], snapshot.Bar);
                Assert.Equal(count, index);
            }
        }
    }

    [SkippableFact]
    public async Task CertificateRejectionInputPrecedenceRecoveryAndRetention()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var bars = Enumerable.Range(0, 513).Select(i => new Bar(default, 0, 1, -1, (i % 31 - 15) / 8d, 1)).ToArray();
        var original = bars.ToArray(); var indicator = new EndpointWeightedAverage(20);
        var builder = Build(bars, indicator); using var saved = await builder.BuildAsync();
        foreach (double value in new[] { double.Epsilon, double.MaxValue, .1 })
        {
            bars[0] = new Bar(default, 0, 1, -1, value, 1);
            await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync());
            Assert.Null(builder.LastExecution);
        }
        bars[^1] = new Bar(default, 0, 1, -1, 1, double.NaN);
        var expected = await Record.ExceptionAsync(() => Build(bars, indicator).ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync());
        var actual = await Record.ExceptionAsync(() => builder.BuildAsync());
        Assert.NotNull(expected); Assert.NotNull(actual);
        Assert.Equal(expected.GetType(), actual.GetType()); Assert.Equal(expected.Message, actual.Message);
        Array.Copy(original, bars, bars.Length); using var recovered = await builder.BuildAsync();
        Assert.Equal(saved[indicator].ToArray(), recovered[indicator].ToArray());
        foreach (int period in new[] { 32768, 65535, int.MaxValue })
            await Assert.ThrowsAsync<NotSupportedException>(() => Build(bars, new EndpointWeightedAverage(period)).BuildAsync());
        Array.Clear(bars); int index = 0;
        await foreach (var snapshot in saved) Assert.Equal(original[index++], snapshot.Bar);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancel.Token));
    }

    private static StockIndicatorBuilder Build(Bar[] bars, IIndicator indicator, IndicatorHistoryMode history = IndicatorHistoryMode.Full) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).ConfigureHistory(history)
            .ConfigureExecution(IndicatorExecutionBackend.Gpu);
}

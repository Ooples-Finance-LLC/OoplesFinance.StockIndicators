using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class TensorsTrueRangeGpuExecutionTests
{
    private static StockIndicatorBuilder Builder(Bar[] bars, IIndicator indicator, IndicatorHistoryMode history = IndicatorHistoryMode.Full) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).ConfigureHistory(history)
            .ConfigureExecution(IndicatorExecutionBackend.Gpu);

    [SkippableTheory]
    [InlineData(1)] [InlineData(20)] [InlineData(int.MaxValue)]
    public async Task DeviceRangePreservesFirstBarSignedZerosAndWorkerBoundaries(int period)
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        double[] values = [-0d, 0d, double.Epsilon, -double.Epsilon, double.MaxValue / 4, -double.MaxValue / 4, .1, 1, -1];
        foreach (int count in new[] { 1, 2, 511, 8193 })
        foreach (bool zeros in new[] { false, true })
        {
            var bars = Enumerable.Range(0, count).Select(i => zeros
                ? new Bar(default, 0, -0d, 0d, i % 2 == 0 ? -0d : 0d, 1)
                : new Bar(default, 0, values[i % values.Length], values[(i + 1) % values.Length], values[(i + 3) % values.Length], 1)).ToArray();
            var indicator = new TrueRange(period);
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                using var cpu = await Builder(bars, indicator, history).ConfigureExecution(IndicatorExecutionBackend.Cpu).ConfigureBehavior(_ => { }).BuildAsync();
                var builder = Builder(bars, indicator, history);
                using var gpu = await builder.BuildAsync();
                Assert.Equal(IndicatorExecutionBackend.Gpu, builder.LastExecution!.Backend);
                Assert.Equal(cpu[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits), gpu[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits));
                Assert.Equal(cpu.Latest.IsWarmedUp, gpu.Latest.IsWarmedUp);
                if (history == IndicatorHistoryMode.Full)
                {
                    int i = 0; await foreach (var snapshot in gpu) Assert.Equal(bars[i++], snapshot.Bar);
                    Assert.Equal(count, i);
                }
            }
        }
    }

    [SkippableFact]
    public async Task FailureOrderRecoveryCancellationAndConcurrentRunsPreserveOwnership()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var bars = Enumerable.Range(0, 513).Select(i => new Bar(default, 0, i % 11 + 2, i % 11, i % 11 + 1, 1)).ToArray();
        var indicator = new TrueRange(1); var builder = Builder(bars, indicator);
        using var saved = await builder.BuildAsync(); var original = bars.ToArray();
        foreach (bool invalid in new[] { false, true })
        {
            bars[0] = new Bar(default, 0, double.MaxValue, -double.MaxValue, 0, 1);
            if (invalid) bars[^1] = new Bar(default, 0, 1, 0, .5, double.NaN);
            var expected = await Record.ExceptionAsync(() => Builder(bars, indicator).ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync());
            var actual = await Record.ExceptionAsync(() => builder.BuildAsync());
            Assert.NotNull(expected); Assert.NotNull(actual);
            Assert.Equal(expected.GetType(), actual.GetType()); Assert.Equal(expected.Message, actual.Message); Assert.Null(builder.LastExecution);
        }
        original.CopyTo(bars, 0);
        using var recovered = await builder.BuildAsync(); Assert.Equal(saved[indicator].ToArray(), recovered[indicator].ToArray());
        await Task.WhenAll(Enumerable.Range(0, 4).Select(worker => Task.Run(async () =>
        {
            var concurrent = bars.Select(b => new Bar(default, b.Open, b.High + worker, b.Low - worker, b.Close, b.Volume)).ToArray();
            var other = new TrueRange(1);
            using var cpu = await Builder(concurrent, other).ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
            var selected = Builder(concurrent, other); using var gpu = await selected.BuildAsync();
            Assert.Equal(IndicatorExecutionBackend.Gpu, selected.LastExecution!.Backend);
            Assert.Equal(cpu[other].ToArray(), gpu[other].ToArray());
        })));
        Array.Clear(bars);
        int i = 0; await foreach (var snapshot in saved) Assert.Equal(original[i++], snapshot.Bar);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancel.Token));
    }

    [Fact]
    public async Task UnsupportedGraphsAndEmptyInputsStillReject()
    {
        Assert.False(ValuesBarExecution.SupportsGpu([new TrueRange(1).Of(new Sma(3))]));
        Assert.False(ValuesBarExecution.SupportsGpu([new TrueRange(1), new HighestHigh(3)]));
        await Assert.ThrowsAsync<NotSupportedException>(() => Builder([], new TrueRange(1)).BuildAsync());
    }
}

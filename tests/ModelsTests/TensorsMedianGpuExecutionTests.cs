using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class TensorsMedianGpuExecutionTests
{
    private static StockIndicatorBuilder Build(Bar[] bars, IIndicator indicator) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).ConfigureExecution(IndicatorExecutionBackend.Gpu);

    [SkippableTheory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task PrivateWindowPreservesWarmupInsertionAndAllOutputBits(int shape)
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        double[] edges = [-0d, 0d, double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon, 1, -1];
        foreach (int period in new[] { 1, 2, 4, 20, 64 })
        foreach (int count in new[] { 1, 19, 20, 21, 65, 257 })
        {
            var bars = Enumerable.Range(0, count).Select(i => new Bar(default, 0, 1, -1,
                shape == 0 ? (i % 2 == 0 ? -0d : 0d) : shape == 1 ? edges[(i * 5 + i / 11) % edges.Length] : (i % 37 - 18) / 8d, 1)).ToArray();
            var indicator = new RollingPercentile(period);
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                var builder = Build(bars, indicator).ConfigureHistory(history);
                using var cpu = await builder.ConfigureExecution(IndicatorExecutionBackend.Cpu).ConfigureBehavior(_ => { }).BuildAsync();
                builder = Build(bars, indicator).ConfigureHistory(history);
                using var gpu = await builder.BuildAsync();
                Assert.Equal(IndicatorExecutionBackend.Gpu, builder.LastExecution!.Backend);
                Assert.Equal(cpu[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits), gpu[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits));
                Assert.Equal(cpu.Latest.IsWarmedUp, gpu.Latest.IsWarmedUp);
                if (history == IndicatorHistoryMode.Full)
                {
                    int i = 0;
                    await foreach (var snapshot in gpu) Assert.Equal(bars[i++], snapshot.Bar);
                    Assert.Equal(count, i);
                }
            }
        }
    }

    [SkippableFact]
    public async Task DeviceMedianPreservesOwnedValuesAndFailureRecovery()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var bars = Enumerable.Range(0, 513).Select(i => new Bar(default, 0, 1, -1, i % 23, 1)).ToArray();
        var indicator = new RollingPercentile(20);
        var builder = Build(bars, indicator);
        using var saved = await builder.BuildAsync(); var original = bars[256];
        bars[256] = new Bar(default, 0, 1, -1, 0, double.NaN);
        var expected = await Record.ExceptionAsync(() => builder.ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync());
        var actual = await Record.ExceptionAsync(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
        Assert.NotNull(expected); Assert.NotNull(actual);
        Assert.Equal(expected.GetType(), actual.GetType()); Assert.Equal(expected.Message, actual.Message); Assert.Null(builder.LastExecution);
        bars[256] = original;
        using var recovered = await builder.BuildAsync(); Assert.Equal(saved[indicator].ToArray(), recovered[indicator].ToArray());
        var last = bars[^1]; Array.Clear(bars); Assert.Equal(last, saved.Latest.Bar);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancel.Token));
    }

    [Fact]
    public async Task UnsupportedMedianShapesRejectRatherThanUsingCpu()
    {
        foreach (var indicator in new[] { new RollingPercentile(3), new RollingPercentile(66),
            new RollingPercentile(20, .125), new RollingPercentile(5, .25) })
        {
            Assert.False(ValuesBarExecution.SupportsGpu([indicator]));
            await Assert.ThrowsAsync<NotSupportedException>(() => Build([new Bar(default, 0, 1, -1, 0, 1)], indicator).BuildAsync());
        }
        Assert.True(ValuesBarExecution.SupportsGpu([new RollingPercentile(1, .5, true)]));
        Assert.True(ValuesBarExecution.SupportsGpu([new RollingPercentile(20, .5, true)]));
        Assert.False(ValuesBarExecution.SupportsGpu([new RollingPercentile(20).Of(new Sma(3))]));
        Assert.False(ValuesBarExecution.SupportsGpu([new RollingPercentile(20), new HighestHigh(3)]));
        await Assert.ThrowsAsync<NotSupportedException>(() => Build([], new RollingPercentile(20)).BuildAsync());
    }

    [SkippableTheory]
    [InlineData(2)] [InlineData(20)] [InlineData(64)]
    public async Task CertifiedStartupMeanAndMedianShareCachedKernel(int period)
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        foreach (int count in new[] { 1, period - 1, period, period + 3, 257 })
        {
            var bars = Enumerable.Range(0, count).Select(i => new Bar(default, 0, 1, -1,
                i < period ? (i % 7 - 3) / 8d : i % 3 == 0 ? double.MaxValue : i % 3 == 1 ? double.Epsilon : -double.MaxValue, 1)).ToArray();
            foreach (bool average in new[] { false, true, false })
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                var indicator = new RollingPercentile(period, .5, average);
                using var cpu = await Build(bars, indicator).ConfigureHistory(history)
                    .ConfigureExecution(IndicatorExecutionBackend.Cpu).ConfigureBehavior(_ => { }).BuildAsync();
                var builder = Build(bars, indicator).ConfigureHistory(history);
                using var gpu = await builder.BuildAsync();
                Assert.Equal(IndicatorExecutionBackend.Gpu, builder.LastExecution!.Backend);
                Assert.Equal(cpu[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits),
                    gpu[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits));
                Assert.Equal(cpu.Latest.IsWarmedUp, gpu.Latest.IsWarmedUp);
            }
        }
    }

    [SkippableFact]
    public async Task StartupCertificateRejectsAfterInputValidationAndRecovers()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var bars = Enumerable.Range(0, 32).Select(i => new Bar(default, 0, 1, -1, i == 1 ? double.Epsilon : 1, 1)).ToArray();
        var indicator = new RollingPercentile(20, .5, true);
        var builder = Build(bars, indicator);
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync());
        Assert.Null(builder.LastExecution);
        var last = bars[^1]; bars[^1] = new Bar(default, 0, 1, -1, 1, double.NaN);
        var cpuFailure = await Record.ExceptionAsync(() => Build(bars, indicator).ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync());
        var gpuFailure = await Record.ExceptionAsync(() => builder.BuildAsync());
        Assert.NotNull(cpuFailure); Assert.NotNull(gpuFailure);
        Assert.Equal(cpuFailure.GetType(), gpuFailure.GetType()); Assert.Equal(cpuFailure.Message, gpuFailure.Message);
        bars[^1] = last; bars[1] = last;
        using var recovered = await builder.BuildAsync();
        Assert.Equal(IndicatorExecutionBackend.Gpu, builder.LastExecution!.Backend);
        Assert.All(recovered[indicator].ToArray(), value => Assert.Equal(1, value));
    }

    [SkippableTheory]
    [InlineData(.25, false)] [InlineData(.25, true)]
    [InlineData(.75, false)] [InlineData(.75, true)]
    public async Task QuarterInterpolationPreservesAllBits(double fraction, bool average)
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        foreach (int period in new[] { 1, 2, 3, 4, 20, 64 })
        foreach (int count in new[] { 1, period, period + 3, 257 })
        {
            var bars = Enumerable.Range(0, count).Select(i => new Bar(default, 0, 1, -1,
                i % 13 == 6 ? (i % 2 == 0 ? -0d : 0d) : (i % 13 - 6) / 8d, 1)).ToArray();
            var indicator = new RollingPercentile(period, fraction, average);
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                using var cpu = await Build(bars, indicator).ConfigureHistory(history)
                    .ConfigureExecution(IndicatorExecutionBackend.Cpu).ConfigureBehavior(_ => { }).BuildAsync();
                var builder = Build(bars, indicator).ConfigureHistory(history);
                using var gpu = await builder.BuildAsync();
                Assert.Equal(IndicatorExecutionBackend.Gpu, builder.LastExecution!.Backend);
                Assert.Equal(cpu[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits),
                    gpu[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits));
            }
        }
    }

    [SkippableFact]
    public async Task InterpolationCertificateIncludesMatureInputsAndRecoversAcrossFractions()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var bars = Enumerable.Range(0, 32).Select(i => new Bar(default, 0, 1, -1, i == 31 ? double.Epsilon : 1, 1)).ToArray();
        foreach (double fraction in new[] { .25, .5, .75, .5, .25 })
        {
            var indicator = new RollingPercentile(20, fraction, true);
            var builder = Build(bars, indicator);
            if (fraction != .5)
            {
                await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync());
                Assert.Null(builder.LastExecution);
            }
            else
            {
                using var run = await builder.BuildAsync();
                Assert.Equal(IndicatorExecutionBackend.Gpu, builder.LastExecution!.Backend);
                Assert.All(run[indicator].ToArray(), value => Assert.Equal(1, value));
            }
            var valid = Enumerable.Range(0, 32).Select(i => new Bar(default, 0, 1, -1, i, 1)).ToArray();
            using var cpu = await Build(valid, indicator).ConfigureExecution(IndicatorExecutionBackend.Cpu)
                .ConfigureBehavior(_ => { }).BuildAsync();
            using var recovered = await builder.ConfigureSource(Bars.From(valid)).BuildAsync();
            Assert.Equal(IndicatorExecutionBackend.Gpu, builder.LastExecution!.Backend);
            Assert.Equal(cpu[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits),
                recovered[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits));
        }
    }
}

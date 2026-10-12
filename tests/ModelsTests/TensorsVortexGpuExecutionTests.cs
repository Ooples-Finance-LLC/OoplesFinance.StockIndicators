using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class TensorsVortexGpuExecutionTests
{
    private static Bar[] Data(int count) => Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i),
        0, i % 64 < 32 ? 1 : (i % 71 - 30) / 8d,
        i % 64 < 32 ? 1 : (i % 71 - 40) / 8d,
        i % 64 < 32 ? 1 : (i % 71 - 35) / 8d, 1)).ToArray();

    [SkippableTheory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(20)]
    [InlineData(63)] [InlineData(64)] [InlineData(65)] [InlineData(4096)] [InlineData(int.MaxValue)]
    public async Task DeviceValuesAndPresenceMatchExactCpuAcrossStartupFlatWindowsAndBlocks(int period)
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        foreach (int count in new[] { 1, 63, 64, 65, 257 })
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var bars = Data(count);
            if (count > 3) bars[2] = new(default, 0, -1, 1, -0d, 1);
            var indicator = new WindowVortex(period);
            using var cpu = await Build(bars, indicator, history).ConfigureExecution(IndicatorExecutionBackend.Cpu)
                .ConfigureBehavior(_ => { }).BuildAsync();
            var builder = Build(bars, indicator, history);
            using var gpu = await builder.BuildAsync();
            Assert.Equal(IndicatorExecutionBackend.Gpu, builder.LastExecution!.Backend);
            gpu.Dispose();
            foreach (var output in indicator.Outputs)
                Assert.Equal(cpu[output].ToArray().Select(BitConverter.DoubleToInt64Bits),
                    gpu[output].ToArray().Select(BitConverter.DoubleToInt64Bits));
            Assert.Equal(cpu.Latest.IsWarmedUp, gpu.Latest.IsWarmedUp);
            if (history == IndicatorHistoryMode.Full)
            {
                int index = 0; await foreach (var row in gpu) Assert.Equal(bars[index++], row.Bar);
                Assert.Equal(count, index);
            }
        }
    }

    [SkippableFact]
    public async Task LargeCompletedWindowAndAllMissingWideInputsKeepTheirContracts()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        {
            var bars = Data(4098); var indicator = new WindowVortex(4096);
            using var cpu = await Build(bars, indicator).ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
            using var gpu = await Build(bars, indicator).BuildAsync();
            foreach (var output in indicator.Outputs) Assert.Equal(cpu[output].ToArray(), gpu[output].ToArray());
            var missing = new WindowVortex(int.MaxValue);
            bars = [new(default, 0, 1, -1, double.MaxValue, 1), new(default, 0, 1, -1, double.Epsilon, 1)];
            using var emptyWindow = await Build(bars, missing).BuildAsync();
            foreach (var output in missing.Outputs) Assert.All(emptyWindow[output].ToArray(), value => Assert.Equal(0, value));
        }
    }

    [SkippableFact]
    public async Task FusedOutputsSurviveWorkspaceReuseByOtherKernelsAndMissingWindows()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var bars = Data(257);
        var vortex = new WindowVortex(14);
        using var expected = await Build(bars, vortex).ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
        using var retained = await Build(bars, vortex).BuildAsync();
        foreach (IIndicator indicator in new IIndicator[] { new TrueRange(14), new WindowVortex(4096), vortex })
        {
            var builder = Build(bars, indicator, IndicatorHistoryMode.LatestOnly);
            using var actual = await builder.BuildAsync();
            Assert.Equal(IndicatorExecutionBackend.Gpu, builder.LastExecution!.Backend);
            using var cpu = await Build(bars, indicator).ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
            foreach (var output in indicator.Outputs)
                Assert.Equal(cpu[output].ToArray().Select(BitConverter.DoubleToInt64Bits),
                    actual[output].ToArray().Select(BitConverter.DoubleToInt64Bits));
        }
        retained.Dispose();
        foreach (var output in vortex.Outputs)
            Assert.Equal(expected[output].ToArray().Select(BitConverter.DoubleToInt64Bits),
                retained[output].ToArray().Select(BitConverter.DoubleToInt64Bits));
    }

    [SkippableFact]
    public async Task RequiredGpuValidatesAllFieldsRejectsUncertifiedInputsAndRecovers()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var bars = Data(257); var original = bars.ToArray();
        var indicator = new WindowVortex(20);
        var builder = Build(bars, indicator);
        using var saved = await builder.BuildAsync();
        foreach (double unsupported in new[] { .1, double.Epsilon, double.MaxValue })
        {
            bars[0] = new(default, 0, 1, -1, unsupported, 1);
            await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync());
            Assert.Null(builder.LastExecution);
        }
        foreach (var uncertified in new[] { new Bar(default, 0, .1, -1, 1, 1), new Bar(default, 0, 1, .1, 1, 1) })
        {
            Array.Copy(original, bars, bars.Length); bars[0] = uncertified;
            await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync());
            Assert.Null(builder.LastExecution);
        }
        bars[^1] = new(default, 0, double.NaN, -1, 1, 1);
        var expected = await Record.ExceptionAsync(() => Build(bars, indicator).ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync());
        var actual = await Record.ExceptionAsync(() => builder.BuildAsync());
        Assert.NotNull(expected); Assert.NotNull(actual);
        Assert.Equal(expected.GetType(), actual.GetType()); Assert.Equal(expected.Message, actual.Message);
        Array.Copy(original, bars, bars.Length);
        using var recovered = await builder.BuildAsync();
        foreach (var output in indicator.Outputs) Assert.Equal(saved[output].ToArray(), recovered[output].ToArray());
        await Assert.ThrowsAsync<NotSupportedException>(() => Build([], indicator).BuildAsync());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
        Array.Clear(bars); saved.Dispose();
        int index = 0; await foreach (var row in saved) Assert.Equal(original[index++], row.Bar);
    }

    private static StockIndicatorBuilder Build(Bar[] bars, IIndicator indicator, IndicatorHistoryMode history = IndicatorHistoryMode.Full) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).ConfigureHistory(history)
            .ConfigureExecution(IndicatorExecutionBackend.Gpu);
}

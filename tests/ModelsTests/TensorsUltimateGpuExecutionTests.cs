using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class TensorsUltimateGpuExecutionTests
{
    private static Bar[] Data(int count) => Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i),
        123 + i, i % 64 < 32 ? 1 : (i % 71 - 30) / 8d,
        i % 64 < 32 ? 1 : (i % 71 - 40) / 8d,
        i % 64 < 32 ? 1 : (i % 71 - 35) / 8d, 1)).ToArray();

    [SkippableTheory]
    [InlineData(1, 2, 3, false)] [InlineData(7, 14, 28, true)]
    [InlineData(1, 1, 1, true)] [InlineData(14, 7, 14, true)]
    [InlineData(63, 64, 65, false)] [InlineData(4094, 4095, 4096, false)]
    [InlineData(1, 2, int.MaxValue, false)]
    public async Task DeviceRatiosAndPresenceMatchExactCpuAcrossWindowsAndBlocks(int a, int b, int c, bool zero)
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        foreach (int count in new[] { 1, 63, 64, 65, 257 })
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var bars = Data(count);
            if (count > 3) bars[2] = new(default, 0, -1, 1, -0d, 1);
            var indicator = new WindowUltimateOscillator(a, b, c, zero);
            using var cpu = await Build(bars, indicator, history).ConfigureExecution(IndicatorExecutionBackend.Cpu)
                .ConfigureBehavior(_ => { }).BuildAsync();
            var builder = Build(bars, indicator, history);
            using var gpu = await builder.BuildAsync();
            Assert.Equal(IndicatorExecutionBackend.Gpu, builder.LastExecution!.Backend);
            gpu.Dispose();
            EqualOutputs(indicator, cpu, gpu);
            Assert.Equal(cpu.Latest.IsWarmedUp, gpu.Latest.IsWarmedUp);
            Assert.Equal(bars[^1], gpu.Latest.Bar);
            if (history == IndicatorHistoryMode.Full)
            {
                int index = 0; await foreach (var row in gpu) Assert.Equal(bars[index++], row.Bar);
                Assert.Equal(count, index);
            }
        }
    }

    [SkippableTheory]
    [InlineData(-1074)] [InlineData(0)] [InlineData(900)]
    public async Task ExactCertificateBoundaryAndSignedPressureExecuteOrReject(int scale)
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        foreach (bool zero in new[] { false, true })
        {
            var bars = Enumerable.Range(0, 257).Select(i => new Bar(default, 123,
                Math.ScaleB(i % 3 == 0 ? -418 : 418, scale), Math.ScaleB(i % 3 == 0 ? 418 : -418, scale),
                Math.ScaleB(i % 5 == 0 ? 1 : i % 2 == 0 ? 418 : -418, scale), 1)).ToArray();
            var indicator = new WindowUltimateOscillator(7, 14, 28, zero);
            using var cpu = await Build(bars, indicator).ConfigureExecution(IndicatorExecutionBackend.Cpu)
                .ConfigureBehavior(_ => { }).BuildAsync();
            var builder = Build(bars, indicator);
            using var gpu = await builder.BuildAsync();
            Assert.Equal(IndicatorExecutionBackend.Gpu, builder.LastExecution!.Backend);
            EqualOutputs(indicator, cpu, gpu);
            bars[0] = new(default, 0, Math.ScaleB(419, scale), 0, Math.ScaleB(1, scale), 1);
            await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync());
            Assert.Null(builder.LastExecution);
        }
    }

    [SkippableFact]
    public async Task LongCompletedAndAllMissingWideWindowsExecuteOnDevice()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var bars = Enumerable.Range(0, 4098).Select(i => new Bar(default, 0, 1, 0, i % 2, 1)).ToArray();
        var indicator = new WindowUltimateOscillator(4094, 4095, 4096);
        using var cpu = await Build(bars, indicator).ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
        var builder = Build(bars, indicator);
        using var gpu = await builder.BuildAsync();
        Assert.Equal(IndicatorExecutionBackend.Gpu, builder.LastExecution!.Backend);
        EqualOutputs(indicator, cpu, gpu);
        var missing = new WindowUltimateOscillator(1, 2, int.MaxValue);
        bars = [new(default, 0, 1, -1, double.MaxValue, 1), new(default, 0, 1, -1, double.Epsilon, 1)];
        builder = Build(bars, missing);
        using var empty = await builder.BuildAsync();
        Assert.Equal(IndicatorExecutionBackend.Gpu, builder.LastExecution!.Backend);
        foreach (var output in missing.Outputs) Assert.All(empty[output].ToArray(), value => Assert.Equal(0, value));
    }

    [SkippableFact]
    public async Task PackedWorkspaceReusePreservesRetainedResultsAndOriginalBars()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var bars = Data(257); var original = bars.ToArray();
        var ultimate = new WindowUltimateOscillator();
        using var expected = await Build(bars, ultimate).ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
        using var retained = await Build(bars, ultimate).BuildAsync();
        foreach (IIndicator indicator in new IIndicator[] { new WindowVortex(14), ultimate, new TrueRange(14),
            ultimate, new Sma(14), new StrictHaramiCandle(), new WindowUltimateOscillator(1, 2, int.MaxValue), ultimate })
        {
            var builder = Build(bars, indicator, IndicatorHistoryMode.LatestOnly);
            using var actual = await builder.BuildAsync();
            Assert.Equal(IndicatorExecutionBackend.Gpu, builder.LastExecution!.Backend);
            using var cpu = await Build(bars, indicator).ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
            EqualOutputs(indicator, cpu, actual);
        }
        Array.Clear(bars); retained.Dispose();
        EqualOutputs(ultimate, expected, retained);
        int index = 0; await foreach (var row in retained) Assert.Equal(original[index++], row.Bar);
        Assert.Equal(original.Length, index);
    }

    [SkippableFact]
    public async Task RequiredGpuRejectsWideInputsPreservesValidationOrderAndRecovers()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var bars = Data(257); var original = bars.ToArray();
        var indicator = new WindowUltimateOscillator(); var builder = Build(bars, indicator);
        using var saved = await builder.BuildAsync();
        foreach (double unsupported in new[] { .1, double.Epsilon, double.MaxValue })
        {
            bars[0] = new(default, 0, 1, -1, unsupported, 1);
            await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync());
            Assert.Null(builder.LastExecution);
        }
        // A later invalid raw field takes precedence over the earlier failed certificate.
        foreach (var invalid in new[] { new Bar(default, double.NaN, 1, 0, 1, 1),
            new Bar(default, 0, 1, 0, 1, double.PositiveInfinity) })
        {
            bars[^1] = invalid;
            var expected = await Record.ExceptionAsync(() => Build(bars, indicator).ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync());
            var actual = await Record.ExceptionAsync(() => builder.BuildAsync());
            Assert.NotNull(expected); Assert.NotNull(actual);
            Assert.Equal(expected.GetType(), actual.GetType()); Assert.Equal(expected.Message, actual.Message);
        }
        Array.Copy(original, bars, bars.Length);
        using var recovered = await builder.BuildAsync();
        EqualOutputs(indicator, saved, recovered);
        await Assert.ThrowsAsync<NotSupportedException>(() => Build([], indicator).BuildAsync());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
    }

    private static void EqualOutputs(IIndicator indicator, IIndicatorRun expected, IIndicatorRun actual)
    {
        foreach (var output in indicator.Outputs)
            Assert.Equal(expected[output].ToArray().Select(BitConverter.DoubleToInt64Bits),
                actual[output].ToArray().Select(BitConverter.DoubleToInt64Bits));
    }

    private static StockIndicatorBuilder Build(Bar[] bars, IIndicator indicator, IndicatorHistoryMode history = IndicatorHistoryMode.Full) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).ConfigureHistory(history)
            .ConfigureExecution(IndicatorExecutionBackend.Gpu);
}

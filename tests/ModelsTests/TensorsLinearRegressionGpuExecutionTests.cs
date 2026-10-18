using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class TensorsLinearRegressionGpuExecutionTests
{
    private static readonly WindowRegressionOutput[] Outputs = [WindowRegressionOutput.Endpoint,
        WindowRegressionOutput.Forecast, WindowRegressionOutput.Slope, WindowRegressionOutput.Intercept];
    private static Bar[] Data(int count) => Enumerable.Range(0, count).Select(i => new Bar(default,
        0, 1, -1, i % 7 == 0 ? -0d : (i % 31 - 15) / 8d, 1)).ToArray();

    [SkippableTheory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(20)]
    [InlineData(65)] [InlineData(4096)] [InlineData(32767)]
    public async Task DeviceRegressionMatchesExactCpuAcrossStartupAndWorkerBoundaries(int period)
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        foreach (int count in new[] { 1, 63, 64, 65, 513 })
        foreach (var output in Outputs)
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var bars = Data(count);
            if (period == 1) bars[0] = new Bar(default, 0, 1, -1, double.Epsilon, 1);
            var indicator = new WindowLinearRegression(period, output);
            using var cpu = await Build(bars, indicator, history).ConfigureExecution(IndicatorExecutionBackend.Cpu)
                .ConfigureBehavior(_ => { }).BuildAsync();
            var builder = Build(bars, indicator, history);
            using var gpu = await builder.BuildAsync(); gpu.Dispose();
            Assert.Equal(IndicatorExecutionBackend.Gpu, builder.LastExecution!.Backend);
            Assert.Equal(cpu[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits),
                gpu[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits));
            Assert.Equal(cpu.Latest.IsWarmedUp, gpu.Latest.IsWarmedUp);
            if (history == IndicatorHistoryMode.Full)
            {
                int index = 0; await foreach (var row in gpu) Assert.Equal(bars[index++], row.Bar);
                Assert.Equal(count, index);
            }
        }
    }

    [SkippableFact]
    public async Task LargestCertifiedWindowExercisesExactDenominatorsAndEviction()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var bars = Data(32769);
        foreach (var output in Outputs)
        {
            var indicator = new WindowLinearRegression(32767, output);
            using var cpu = await Build(bars, indicator).ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
            using var gpu = await Build(bars, indicator).BuildAsync();
            Assert.Equal(cpu[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits),
                gpu[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits));
        }
    }

    [SkippableFact]
    public async Task RequiredGpuRejectsUnsupportedShapesAndDataThenRecovers()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var bars = Data(513); var original = bars.ToArray();
        var indicator = new WindowLinearRegression(20, WindowRegressionOutput.Forecast);
        var builder = Build(bars, indicator); using var saved = await builder.BuildAsync();
        foreach (double unsupported in new[] { .1, double.Epsilon, double.MaxValue })
        {
            bars[0] = new Bar(default, 0, 1, -1, unsupported, 1);
            await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync());
            Assert.Null(builder.LastExecution);
        }
        bars[^1] = new Bar(default, 0, 1, -1, 1, double.NaN);
        var expected = await Record.ExceptionAsync(() => Build(bars, indicator).ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync());
        var actual = await Record.ExceptionAsync(() => builder.BuildAsync());
        Assert.NotNull(expected); Assert.NotNull(actual);
        Assert.Equal(expected.GetType(), actual.GetType()); Assert.Equal(expected.Message, actual.Message);
        Array.Copy(original, bars, bars.Length);
        foreach (int period in new[] { 32768, 65535, int.MaxValue })
            await Assert.ThrowsAsync<NotSupportedException>(() => Build(bars, new WindowLinearRegression(period)).BuildAsync());
        await Assert.ThrowsAsync<NotSupportedException>(() => Build(bars, new WindowLinearRegression(20, WindowRegressionOutput.Angle)).BuildAsync());
        using var recovered = await builder.BuildAsync();
        Assert.Equal(saved[indicator].ToArray(), recovered[indicator].ToArray());
        Array.Clear(bars); saved.Dispose();
        int index = 0; await foreach (var row in saved) Assert.Equal(original[index++], row.Bar);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancel.Token));
    }

    private static StockIndicatorBuilder Build(Bar[] bars, IIndicator indicator, IndicatorHistoryMode history = IndicatorHistoryMode.Full) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).ConfigureHistory(history)
            .ConfigureExecution(IndicatorExecutionBackend.Gpu);
}

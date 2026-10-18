using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class TensorsAffineConvolutionGpuExecutionTests
{
    public static IEnumerable<object[]> Kernels => new double[][]
    {
        [1], [0], [int.MaxValue], [0, 0, 0], [1, 2, 3], [3, 2, 1],
        [0, 1, 2], [1, -1], [-3, -2, -1], [-2, -1, 0, 1, 2], [2, -1, -4],
        [1_000_000_000, 1_000_000_000], Enumerable.Range(0, 65).Select(i => 64d - 2 * i).ToArray()
    }.Select(kernel => new object[] { kernel });

    [SkippableTheory]
    [MemberData(nameof(Kernels))]
    public async Task SignedAndZeroMassDeviceWindowsMatchExactCpu(double[] kernel)
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        foreach (int count in new[] { 1, 63, 64, 65, 513 })
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var bars = Enumerable.Range(0, count).Select(i => new Bar(default, 0, 1, -1,
                i % 7 == 0 ? -0d : (i % 31 - 15) / 8d, 1)).ToArray();
            if (kernel.Length == 1) bars[0] = new Bar(default, 0, 1, -1, double.Epsilon, 1);
            var indicator = new NormalizedConvolution(kernel);
            using var cpu = await Build(bars, indicator, history).ConfigureExecution(IndicatorExecutionBackend.Cpu)
                .ConfigureBehavior(_ => { }).BuildAsync();
            var builder = Build(bars, indicator, history); using var gpu = await builder.BuildAsync(); gpu.Dispose();
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
    public async Task UnsupportedCoefficientsAndInputsRejectWithoutCpuFallback()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var bars = Enumerable.Range(0, 65).Select(i => new Bar(default, 0, 1, -1, i % 7, 1)).ToArray();
        foreach (double[] kernel in new double[][] { [1, 2, 4], [.5, 1], [int.MinValue], [int.MaxValue, int.MaxValue] })
        {
            var builder = Build(bars, new NormalizedConvolution(kernel));
            await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync());
            Assert.Null(builder.LastExecution);
        }
        var indicator = new NormalizedConvolution([1, -1]); var valid = bars.ToArray();
        var reusable = Build(bars, indicator); using var saved = await reusable.BuildAsync();
        foreach (double unsupported in new[] { .1, double.Epsilon, double.MaxValue })
        {
            bars[0] = new Bar(default, 0, 1, -1, unsupported, 1);
            await Assert.ThrowsAsync<NotSupportedException>(() => reusable.BuildAsync());
        }
        bars[^1] = new Bar(default, 0, 1, -1, 1, double.NaN);
        var expected = await Record.ExceptionAsync(() => Build(bars, indicator).ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync());
        var actual = await Record.ExceptionAsync(() => reusable.BuildAsync());
        Assert.NotNull(expected); Assert.NotNull(actual); Assert.Equal(expected.GetType(), actual.GetType()); Assert.Equal(expected.Message, actual.Message);
        Array.Copy(valid, bars, bars.Length); using var recovered = await reusable.BuildAsync();
        Assert.Equal(saved[indicator].ToArray(), recovered[indicator].ToArray());
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reusable.BuildAsync(cancel.Token));
    }
    private static StockIndicatorBuilder Build(Bar[] bars, IIndicator indicator, IndicatorHistoryMode history = IndicatorHistoryMode.Full) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).ConfigureHistory(history)
            .ConfigureExecution(IndicatorExecutionBackend.Gpu);
}

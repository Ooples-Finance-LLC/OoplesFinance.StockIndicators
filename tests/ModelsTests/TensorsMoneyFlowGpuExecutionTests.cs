using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class TensorsMoneyFlowGpuExecutionTests
{
    private static Bar[] Data(int count) => Enumerable.Range(0, count).Select(i =>
    {
        double price = 10 + (i * 13 % 101) / 8d;
        return new Bar(DateTime.UnixEpoch.AddMinutes(i), 123 + i, price + 1, price - 1, price, i % 13 + 1);
    }).ToArray();

    [SkippableTheory]
    [InlineData(2, false, 0)] [InlineData(3, true, 0)] [InlineData(14, true, 20)]
    [InlineData(63, false, 0)] [InlineData(64, true, 0)] [InlineData(4096, false, 0)]
    [InlineData(int.MaxValue, false, 0)] [InlineData(2, true, int.MaxValue)]
    public async Task DeviceFlowsMatchExactCpuAcrossStartupSuppressionAndBlocks(int period, bool minimum, int suppressed)
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        foreach (int count in new[] { 1, 63, 64, 65, 257 })
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var bars = Data(count);
            var indicator = new WindowMoneyFlowIndex(period, minimum, suppressed);
            using var cpu = await Build(bars, indicator, history).ConfigureExecution(IndicatorExecutionBackend.Cpu)
                .ConfigureBehavior(_ => { }).BuildAsync();
            var builder = Build(bars, indicator, history);
            using var gpu = await builder.BuildAsync();
            Assert.Equal(IndicatorExecutionBackend.Gpu, builder.LastExecution!.Backend);
            gpu.Dispose(); EqualOutputs(indicator, cpu, gpu);
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
    [InlineData(false)] [InlineData(true)]
    public async Task WideMantissaFlowsAndSubnormalGridsPreserveRounding(bool minimum)
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var random = new Random(62917);
        foreach (int priceSign in new[] { -1, 1 })
        foreach (int volumeSign in new[] { -1, 1 })
        {
            var bars = Enumerable.Range(0, 513).Select(i =>
            {
                double price = priceSign * (1 + random.NextDouble());
                return new Bar(default, 123, price, price, price, volumeSign * (i % 13 + 1));
            }).ToArray();
            await Check(bars, new WindowMoneyFlowIndex(63, minimum));
        }
        foreach (int scale in new[] { -1074, -900, 900 })
        {
            var bars = Data(257).Select(b => new Bar(b.Time, b.Open, Math.ScaleB(b.Close, scale),
                Math.ScaleB(b.Close, scale), Math.ScaleB(b.Close, scale), b.Volume)).ToArray();
            await Check(bars, new WindowMoneyFlowIndex(14, minimum));
            bars = Data(257).Select(b => new Bar(b.Time, b.Open, b.High, b.Low, b.Close, Math.ScaleB(b.Volume, scale))).ToArray();
            await Check(bars, new WindowMoneyFlowIndex(14, minimum));
        }
    }

    [SkippableFact]
    public async Task SignedCancellationRatiosThresholdsAndZeroMatchExactCpu()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        foreach (long total in new long[] { -1, 1, 3 })
        foreach (long volume in new[] { (1L << 50) - 1, (1L << 51) - 1 })
        {
            var bars = new[] { new Bar(default, 0, 0, 0, 0, 1), new Bar(default, 0, 3, 3, 3, volume),
                new Bar(default, 0, 2, 2, 2, (total - 3 * volume) / 2) };
            await Check(bars, new WindowMoneyFlowIndex(2));
        }
        foreach (double volume in new[] { Math.BitDecrement(1d), 1d, Math.BitIncrement(1d), -1d })
        foreach (bool minimum in new[] { false, true })
            await Check([new(default, 7, 0, 0, 0, volume), new(default, 7, 1, 1, 1, volume),
                new(default, 7, 0, 0, 0, volume)], new WindowMoneyFlowIndex(2, minimum));
        await Check([new(default, 2, 2, 2, 2, 1), new(default, 1, 1, 1, 1, -1),
            new(default, 0, 0, 0, 0, 1)], new WindowMoneyFlowIndex(2));
        await Check(Enumerable.Range(0, 4098).Select(i => new Bar(default, 0, 1 + i % 3, 1 + i % 3, 1 + i % 3, 1)).ToArray(),
            new WindowMoneyFlowIndex(4096));
    }

    [SkippableFact]
    public async Task WorkspaceReuseRetainsAllOutputsAndOriginalHistory()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var bars = Data(257); var original = bars.ToArray(); var flow = new WindowMoneyFlowIndex(14);
        using var expected = await Build(bars, flow).ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
        using var retained = await Build(bars, flow).BuildAsync();
        foreach (IIndicator indicator in new IIndicator[] { new WindowUltimateOscillator(), flow, new WindowVortex(14), flow,
            new Sma(14), flow, new StrictHaramiCandle(), new WindowMoneyFlowIndex(int.MaxValue), flow })
            await Check(bars, indicator);
        Array.Clear(bars); retained.Dispose(); EqualOutputs(flow, expected, retained);
        int index = 0; await foreach (var row in retained) Assert.Equal(original[index++], row.Bar);
        Assert.Equal(original.Length, index);
    }

    [SkippableFact]
    public async Task RequiredGpuRejectsWideProductsValidatesOutputsAndRecovers()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var bars = Data(257); var original = bars.ToArray(); var flow = new WindowMoneyFlowIndex(2);
        var builder = Build(bars, flow); using var saved = await builder.BuildAsync();
        foreach (double unsupported in new[] { double.Epsilon, double.MaxValue })
        {
            bars[0] = new(default, 0, unsupported, unsupported, unsupported, 1);
            await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync());
            Assert.Null(builder.LastExecution);
        }
        Array.Copy(original, bars, bars.Length);
        bars[0] = new(default, 0, 1.1, 1.1, 1.1, 1.1);
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync());
        bars[^1] = new(default, double.NaN, 1, 0, 1, 1);
        await EqualFailure(bars, flow);
        Array.Copy(original, bars, bars.Length);
        bars[0] = new(default, 1, 1, 1, 1, 1);
        bars[1] = new(default, 2, 2, 2, 2, 1);
        bars[2] = new(default, 1, 1, 1, 1, -2);
        await EqualFailure(bars, flow); // Certified zero total publishes a rejected infinity.
        bars[^1] = new(default, 0, 1, 1, 1, double.NaN);
        await EqualFailure(bars, flow); // Later invalid input has priority.
        Array.Copy(original, bars, bars.Length);
        using var recovered = await builder.BuildAsync(); EqualOutputs(flow, saved, recovered);
        await Assert.ThrowsAsync<NotSupportedException>(() => Build([], flow).BuildAsync());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
        await Check([new(default, 0, double.MaxValue, -double.MaxValue, double.Epsilon, double.MaxValue)],
            new WindowMoneyFlowIndex(int.MaxValue));
    }

    private static async Task EqualFailure(Bar[] bars, IIndicator indicator)
    {
        var expected = await Record.ExceptionAsync(() => Build(bars, indicator).ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync());
        var actual = await Record.ExceptionAsync(() => Build(bars, indicator).BuildAsync());
        Assert.NotNull(expected); Assert.NotNull(actual);
        Assert.Equal(expected.GetType(), actual.GetType()); Assert.Equal(expected.Message, actual.Message);
    }
    private static async Task Check(Bar[] bars, IIndicator indicator)
    {
        using var expected = await Build(bars, indicator).ConfigureExecution(IndicatorExecutionBackend.Cpu)
            .ConfigureBehavior(_ => { }).BuildAsync();
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var builder = Build(bars, indicator, history);
            using var actual = await builder.BuildAsync();
            Assert.Equal(IndicatorExecutionBackend.Gpu, builder.LastExecution!.Backend);
            EqualOutputs(indicator, expected, actual);
        }
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

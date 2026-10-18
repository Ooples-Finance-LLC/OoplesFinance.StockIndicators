using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedPrefixExtremaTests
{
    [SkippableTheory]
    [InlineData(false, false)] [InlineData(true, false)] [InlineData(false, true)] [InlineData(true, true)]
    public async Task BlockPrefixesKeepOldestBitsAndSnapshots(bool maximum, bool gpu)
    {
        if (gpu) Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        foreach (int count in new[] { 1, 255, 256, 257, 511, 512, 513, 8193 })
        foreach (bool zero in new[] { false, true })
        foreach (bool larger in new[] { false, true })
        {
            double[] edges = [-0d, 0d, double.Epsilon, -double.Epsilon, double.MaxValue, -double.MaxValue, 1, -1];
            var bars = Enumerable.Range(0, count).Select(i => new Bar(default, 0,
                zero ? (i % 2 == 0 ? -0d : 0d) : edges[(i / 251) % edges.Length],
                zero ? (i % 2 == 0 ? 0d : -0d) : edges[(i / 253 + 4) % edges.Length], 0, 1)).ToArray();
            IIndicator indicator = maximum ? new HighestHigh(larger ? int.MaxValue : count) : new LowestLow(larger ? int.MaxValue : count);
            var expected = new double[count];
            double best = maximum ? bars[0].High : bars[0].Low;
            for (int i = 0; i < count; i++)
            {
                double value = maximum ? bars[i].High : bars[i].Low;
                if (maximum ? value > best : value < best) best = value;
                expected[i] = best;
            }
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator)
                    .ConfigureHistory(history).ConfigureExecution(gpu ? IndicatorExecutionBackend.Gpu : IndicatorExecutionBackend.Cpu);
                using var actual = await builder.BuildAsync();
                Assert.Equal(gpu ? IndicatorExecutionBackend.Gpu : IndicatorExecutionBackend.Cpu, builder.LastExecution!.Backend);
                Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), actual[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits));
                if (history == IndicatorHistoryMode.Full)
                {
                    int i = 0;
                    await foreach (var snapshot in actual) Assert.Equal(bars[i++], snapshot.Bar);
                    Assert.Equal(count, i);
                }
            }
        }
    }

    [SkippableFact]
    public async Task PrefixWorkspaceRecoversAndMixedCpuRootsMatchEvaluator()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var bars = Enumerable.Range(0, 1025).Select(i => new Bar(default, 0, i % 29, -(i % 17), 0, 1)).ToArray();
        var prefix = new HighestHigh(bars.Length);
        var rolling = new LowestLow(7);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(prefix)
            .ConfigureExecution(IndicatorExecutionBackend.Gpu);
        using var saved = await builder.BuildAsync();
        double[] expected = saved[prefix].ToArray();
        var original = bars[256]; bars[256] = new Bar(default, 0, 0, 0, 0, double.NaN);
        var cpuFailure = await Record.ExceptionAsync(() => builder.ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync());
        var gpuFailure = await Record.ExceptionAsync(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
        Assert.NotNull(cpuFailure); Assert.NotNull(gpuFailure);
        Assert.Equal(cpuFailure.GetType(), gpuFailure.GetType()); Assert.Equal(cpuFailure.Message, gpuFailure.Message);
        Assert.Null(builder.LastExecution);
        bars[256] = original;
        using var recovered = await builder.BuildAsync();
        Assert.Equal(expected, recovered[prefix].ToArray()); Assert.Equal(expected, saved[prefix].ToArray());
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancel.Token));
        builder.ConfigureExecution(IndicatorExecutionBackend.Cpu).ConfigureIndicators(prefix, rolling);
        using var mixed = await builder.BuildAsync();
        using var reference = await builder.ConfigureBehavior(_ => { }).BuildAsync();
        Assert.Equal(reference[prefix].ToArray(), mixed[prefix].ToArray());
        Assert.Equal(reference[rolling].ToArray(), mixed[rolling].ToArray());
    }
}

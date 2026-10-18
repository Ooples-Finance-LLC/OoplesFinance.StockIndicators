using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class TensorsEngulfingGpuExecutionTests
{
    private static void RequireGpu() => Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);

    [SkippableFact]
    public async Task DevicePatternMatchesIndependentContainmentForAllBodyEndpointsAndStartupSizes()
    {
        RequireGpu();
        double[] endpoints = [-double.MaxValue, -1, -double.Epsilon, -0d, 0d, double.Epsilon, 1, double.MaxValue];
        var all = new List<Bar>();
        foreach (double a in endpoints) foreach (double b in endpoints)
        foreach (double c in endpoints) foreach (double d in endpoints)
        {
            all.Add(new Bar(default, a, 1, -1, b, 1));
            all.Add(new Bar(default, c, 1, -1, d, 1));
        }
        foreach (int count in new[] { 1, 2, 3, 1023, 1024, 1025, all.Count })
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var bars = all.Take(count).ToArray();
            var original = bars.ToArray();
            var expected = Reference(bars);
            var indicator = new EngulfingPattern();
            var builder = Build(bars, indicator, history);
            using var cpu = await builder.ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
            using var gpu = await builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync();
            Assert.Equal(IndicatorExecutionBackend.Gpu, builder.LastExecution!.Backend);
            Assert.False(string.IsNullOrWhiteSpace(builder.LastExecution.DeviceName));
            Bits(expected, cpu[indicator].ToArray());
            Bits(expected, gpu[indicator].ToArray());
            Assert.Equal(cpu.Latest.IsWarmedUp, gpu.Latest.IsWarmedUp);
            Array.Clear(bars);
            using var next = await builder.BuildAsync();
            Bits(expected, gpu[indicator].ToArray());
            Assert.Equal(original[^1], gpu.Latest.Bar);
            if (history == IndicatorHistoryMode.Full)
            {
                int index = 0;
                await foreach (var snapshot in gpu) Assert.Equal(original[index++], snapshot.Bar);
            }
        }
    }

    [SkippableFact]
    public async Task DevicePatternSharesWorkspaceSafelyAndPreservesInputErrorsAndCancellation()
    {
        RequireGpu();
        var bars = Enumerable.Range(0, 1025).Select(i => new Bar(default, i % 5, 10, -10, i % 3, 1)).ToArray();
        await Task.WhenAll(Enumerable.Range(0, 6).Select(n => Task.Run(async () =>
        {
            IIndicator indicator = n % 2 == 0 ? new EngulfingPattern() : new CandleArithmetic(CandleArithmeticOperation.Add);
            var builder = Build(bars, indicator, IndicatorHistoryMode.Full);
            using var cpu = await builder.ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
            using var gpu = await builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync();
            foreach (var output in indicator.Outputs) Bits(cpu[output].ToArray(), gpu[output].ToArray());
        })));
        foreach (int field in Enumerable.Range(0, 5))
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var invalid = bars.ToArray();
            invalid[1023] = new Bar(default, field == 0 ? double.NaN : 0, field == 1 ? double.NaN : 1,
                field == 2 ? double.NaN : 0, field == 3 ? double.NaN : 1, field == 4 ? double.NaN : 1);
            var builder = Build(invalid, new EngulfingPattern(), history);
            var expected = await Record.ExceptionAsync(() => builder.ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync());
            var actual = await Record.ExceptionAsync(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
            Assert.NotNull(expected); Assert.NotNull(actual);
            Assert.Equal(expected.GetType(), actual.GetType()); Assert.Equal(expected.Message, actual.Message);
            Assert.Null(builder.LastExecution);
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancellation.Token));
            Assert.Null(builder.LastExecution);
        }
    }

    [Fact]
    public async Task EmptyComposedAndMixedRequiredGpuPatternsRemainUnsupported()
    {
        var pattern = new EngulfingPattern();
        var builder = Build([], pattern, IndicatorHistoryMode.LatestOnly).ConfigureExecution(IndicatorExecutionBackend.Gpu);
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync());
        Assert.Null(builder.LastExecution);
        builder.ConfigureSource(Bars.From(new[] { new Bar(default, 1, 2, 0, 1, 1) }));
        builder.ConfigureIndicators(pattern, new HighestHigh(3));
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync());
        Assert.Null(builder.LastExecution);
        builder.ConfigureIndicators(new EngulfingPattern().Of(new Sma(3)));
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync());
        Assert.Null(builder.LastExecution);
    }

    private static double[] Reference(Bar[] bars)
    {
        var values = new double[bars.Length];
        for (int i = 2; i < bars.Length; i++)
        {
            var current = bars[i]; var previous = bars[i - 1];
            bool up = current.Close >= current.Open;
            if (up == (previous.Close >= previous.Open)) continue;
            double left = Math.Min(current.Open, current.Close), right = Math.Max(current.Open, current.Close);
            double oldLeft = Math.Min(previous.Open, previous.Close), oldRight = Math.Max(previous.Open, previous.Close);
            if (left <= oldLeft && right >= oldRight && (left < oldLeft || right > oldRight)) values[i] = up ? 100 : -100;
        }
        return values;
    }
    private static StockIndicatorBuilder Build(Bar[] bars, IIndicator indicator, IndicatorHistoryMode history) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).ConfigureHistory(history);
    private static void Bits(double[] expected, double[] actual) => Assert.Equal(
        expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
}

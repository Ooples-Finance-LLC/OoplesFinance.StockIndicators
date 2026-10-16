using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class TensorsExtremeIndexGpuExecutionTests
{
    private static void RequireGpu() => Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
    private static StockIndicatorBuilder Build(Bar[] bars, params IIndicator[] indicators) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicators)
            .ConfigureExecution(IndicatorExecutionBackend.Gpu);

    [SkippableTheory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task DeviceIndicesPreserveIndependentScanStartupOrderingAndOwnership(int shape)
    {
        RequireGpu();
        double[] values = [-0d, 0d, 1, 1, double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon, 0, 0];
        foreach (int period in new[] { 1, 3, 20, int.MaxValue })
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var bars = Enumerable.Range(0, 257).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, 1, -1, values[i % values.Length], 1)).ToArray();
            var original = bars.ToArray();
            WindowExtremeIndex[] indices = shape switch
            {
                0 => [new(period)], 1 => [new(period, true)],
                2 => [new(period), new(period, true)], _ => [new(period, true), new(period)]
            };
            var builder = Build(bars, indices).ConfigureHistory(history);
            using var run = await builder.BuildAsync();
            Assert.Equal(IndicatorExecutionBackend.Gpu, builder.LastExecution!.Backend);
            foreach (var index in indices)
                Assert.Equal(WindowExtremeReference.Indices(bars, period, index.Maximum), run[index].ToArray());
            Array.Clear(bars);
            Assert.Equal(original[^1], run.Latest.Bar);
            if (history == IndicatorHistoryMode.Full)
            {
                int i = 0;
                await foreach (var snapshot in run) Assert.Equal(original[i++], snapshot.Bar);
                Assert.Equal(original.Length, i);
            }
        }
    }

    [SkippableFact]
    public async Task PairSharesWorkspaceSafelyWithArithmeticAndRecoversFromInputFailure()
    {
        RequireGpu();
        var bars = Enumerable.Range(0, 513).Select(i => new Bar(default, i % 7, 1, -1, (i % 31 - 15) / 8d, 1)).ToArray();
        var min = new WindowExtremeIndex(20);
        var max = new WindowExtremeIndex(20, true);
        var pair = Build(bars, max, min).ConfigureHistory(IndicatorHistoryMode.LatestOnly);
        var add = new CandleArithmetic(CandleArithmeticOperation.Add);
        var arithmetic = Build(bars, add);
        var runs = await Task.WhenAll(Task.Run(() => pair.BuildAsync()), Task.Run(() => arithmetic.BuildAsync()));
        using var first = runs[0]; using var sum = runs[1];
        Assert.Equal(bars.Select(b => b.Close + b.Open), sum[add.Value].ToArray());
        var minimum = WindowExtremeReference.Indices(bars, 20, false).ToArray();
        var maximum = WindowExtremeReference.Indices(bars, 20, true).ToArray();
        Assert.Equal(minimum, first[min].ToArray()); Assert.Equal(maximum, first[max].ToArray());
        var original = bars[256];
        for (int field = 0; field < 5; field++)
        {
            bars[256] = new Bar(default, field == 0 ? double.NaN : 0, field == 1 ? double.NaN : 1,
                field == 2 ? double.NaN : -1, field == 3 ? double.NaN : 0, field == 4 ? double.NaN : 1);
            var expected = await Record.ExceptionAsync(() => pair.ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync());
            var actual = await Record.ExceptionAsync(() => pair.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
            Assert.NotNull(expected); Assert.NotNull(actual);
            Assert.Equal(expected.GetType(), actual.GetType()); Assert.Equal(expected.Message, actual.Message);
            Assert.Null(pair.LastExecution);
        }
        bars[256] = original;
        using var recovered = await pair.BuildAsync();
        Assert.Equal(minimum, recovered[min].ToArray()); Assert.Equal(maximum, recovered[max].ToArray());
        Assert.Equal(minimum, first[min].ToArray()); Assert.Equal(maximum, first[max].ToArray());
        using var after = await arithmetic.BuildAsync();
        Assert.Equal(bars.Select(b => b.Close + b.Open), after[add.Value].ToArray());
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pair.BuildAsync(cancellation.Token));
        Assert.Null(pair.LastExecution);
    }

    [Fact]
    public async Task OnlyPlainMatchingOppositeIndexPairsAreSupported()
    {
        var min = new WindowExtremeIndex(20);
        var max = new WindowExtremeIndex(20, true);
        Assert.True(ValuesBarExecution.SupportsGpu([min, max]));
        Assert.True(ValuesBarExecution.SupportsGpu([max, min]));
        foreach (IIndicator[] indicators in new IIndicator[][]
        {
            [min, new WindowExtremeIndex(21, true)], [min, min], [max, max],
            [min.Of(new Sma(3)), max], [min, max, new WindowExtremeIndex(3)]
        })
        {
            Assert.False(ValuesBarExecution.SupportsGpu(indicators));
            await Assert.ThrowsAsync<NotSupportedException>(() => Build([new Bar(default, 0, 1, -1, 0, 1)], indicators).BuildAsync());
        }
        await Assert.ThrowsAsync<NotSupportedException>(() => Build([], min, max).BuildAsync());
    }
}

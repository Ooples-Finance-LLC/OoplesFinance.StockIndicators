using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class TensorsAroonGpuExecutionTests
{
    private static void RequireGpu() => Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
    private static StockIndicatorBuilder Build(Bar[] bars, IIndicator indicator) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).ConfigureExecution(IndicatorExecutionBackend.Gpu);

    [SkippableTheory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public async Task FusedOutputsMatchIndependentScanWithStartupTiesAndOwnedSnapshots(bool extra, bool oldest)
    {
        RequireGpu();
        double[] values = [-0d, 0d, 1, 1, double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon];
        foreach (int period in new[] { 1, 3, 7, 20, 257, int.MaxValue })
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var bars = Enumerable.Range(0, 513).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0,
                values[i % values.Length], values[(i + 3) % values.Length], 0, 1)).ToArray();
            var original = bars.ToArray();
            var indicator = new WindowAroon(period, extra, oldest);
            var builder = Build(bars, indicator).ConfigureHistory(history);
            using var run = await builder.BuildAsync();
            Assert.Equal(IndicatorExecutionBackend.Gpu, builder.LastExecution!.Backend);
            var expected = Scan(bars, indicator);
            for (int slot = 0; slot < 6; slot++)
                Assert.Equal(expected[slot].Select(BitConverter.DoubleToInt64Bits),
                    run[indicator.Outputs[slot]].ToArray().Select(BitConverter.DoubleToInt64Bits));
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
    public async Task ReusedOutputBuffersRemainOwnedAcrossOtherKernelsAndInputFailure()
    {
        RequireGpu();
        var bars = Enumerable.Range(0, 513).Select(i => new Bar(default, i % 7, i % 19, -(i % 13), i % 5, 1)).ToArray();
        var indicator = new WindowAroon(7);
        var builder = Build(bars, indicator);
        var arithmetic = new CandleArithmetic(CandleArithmeticOperation.Add);
        var runs = await Task.WhenAll(Task.Run(() => builder.BuildAsync()), Task.Run(() => Build(bars, arithmetic).BuildAsync()));
        using var saved = runs[0]; using var sum = runs[1];
        Assert.Equal(bars.Select(b => b.Close + b.Open), sum[arithmetic.Value].ToArray());
        var original = bars[256];
        for (int field = 0; field < 5; field++)
        {
            bars[256] = new Bar(default, field == 0 ? double.NaN : 0, field == 1 ? double.NaN : 1,
                field == 2 ? double.NaN : -1, field == 3 ? double.NaN : 0, field == 4 ? double.NaN : 1);
            var expected = await Record.ExceptionAsync(() => builder.ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync());
            var actual = await Record.ExceptionAsync(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
            Assert.NotNull(expected); Assert.NotNull(actual);
            Assert.Equal(expected.GetType(), actual.GetType()); Assert.Equal(expected.Message, actual.Message);
            Assert.Null(builder.LastExecution);
        }
        bars[256] = original;
        using var recovered = await builder.BuildAsync();
        var reference = Scan(bars, indicator);
        for (int slot = 0; slot < 6; slot++)
        {
            Assert.Equal(reference[slot], recovered[indicator.Outputs[slot]].ToArray());
            Assert.Equal(reference[slot], saved[indicator.Outputs[slot]].ToArray());
        }
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancellation.Token));
        Assert.Null(builder.LastExecution);
    }

    [Fact]
    public async Task OnlyPlainNonemptyAroonIsQualified()
    {
        var indicator = new WindowAroon(20);
        Assert.True(ValuesBarExecution.SupportsGpu([indicator]));
        Assert.False(ValuesBarExecution.SupportsGpu([indicator, new WindowExtremeIndex(20)]));
        var sourced = indicator.Of(new Sma(3));
        Assert.False(ValuesBarExecution.SupportsGpu([sourced]));
        await Assert.ThrowsAsync<NotSupportedException>(() => Build([], indicator).BuildAsync());
        await Assert.ThrowsAsync<NotSupportedException>(() => Build([new Bar(default, 0, 1, -1, 0, 1)], sourced).BuildAsync());
    }

    private static double[][] Scan(Bar[] bars, WindowAroon indicator)
    {
        var outputs = Enumerable.Range(0, 6).Select(_ => new double[bars.Length]).ToArray();
        long size = (long)indicator.Period + (indicator.IncludeExtraBar ? 1 : 0);
        for (int i = 0; i < bars.Length; i++)
        {
            if (i + 1L < size) continue;
            int high = i - (int)size + 1, low = high;
            for (int j = high + 1; j <= i; j++)
            {
                if (bars[j].High > bars[high].High || (!indicator.OldestTie && bars[j].High == bars[high].High)) high = j;
                if (bars[j].Low < bars[low].Low || (!indicator.OldestTie && bars[j].Low == bars[low].Low)) low = j;
            }
            outputs[0][i] = (100L * (indicator.Period - (i - high))) / (double)indicator.Period;
            outputs[1][i] = (100L * (indicator.Period - (i - low))) / (double)indicator.Period;
            outputs[2][i] = (100L * (high - low)) / (double)indicator.Period;
            outputs[3][i] = outputs[4][i] = outputs[5][i] = 1;
        }
        return outputs;
    }
}

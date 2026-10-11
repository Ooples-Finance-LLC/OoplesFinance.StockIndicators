using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class TensorsCloseExtremeGpuExecutionTests
{
    private static void RequireGpu() => Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);

    [SkippableTheory]
    [InlineData(false, false)] [InlineData(true, false)] [InlineData(false, true)] [InlineData(true, true)]
    public async Task DeviceCloseExtremaMatchIndependentReferencesAcrossPeriodsAndOwnership(bool maximum, bool candle)
    {
        RequireGpu();
        foreach (int period in new[] { 1, 3, 20, int.MaxValue })
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var bars = Fixture(period == int.MaxValue ? 41 : 1025);
            var original = bars.ToArray();
            var expected = Reference(bars, period, maximum, candle).ToArray();
            var indicator = Create(period, maximum, candle);
            var builder = Build(bars, indicator, history);
            using var cpu = await builder.ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
            using var gpu = await builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync();
            Assert.Equal(IndicatorExecutionBackend.Gpu, builder.LastExecution!.Backend);
            Assert.False(string.IsNullOrWhiteSpace(builder.LastExecution.DeviceName));
            Bits(expected, cpu[indicator].ToArray());
            Bits(expected, gpu[indicator].ToArray());
            Assert.Equal(cpu.Latest.IsWarmedUp, gpu.Latest.IsWarmedUp);
            Array.Clear(bars);
            Assert.Equal(original[^1], gpu.Latest.Bar);
            if (history == IndicatorHistoryMode.Full)
            {
                int index = 0;
                await foreach (var snapshot in gpu) Assert.Equal(original[index++], snapshot.Bar);
                Assert.Equal(original.Length, index);
            }
        }
    }

    [SkippableTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task EqualZeroExtremaSelectNewestSign(bool maximum)
    {
        RequireGpu();
        var bars = Enumerable.Range(0, 257).Select(i => new Bar(default, 0, 1, -1, i % 2 == 0 ? -0d : 0d, 1)).ToArray();
        var indicator = new DecayingWindowExtreme(20, maximum);
        var builder = Build(bars, indicator, IndicatorHistoryMode.LatestOnly).ConfigureExecution(IndicatorExecutionBackend.Gpu);
        using var run = await builder.BuildAsync();
        Assert.Equal(IndicatorExecutionBackend.Gpu, builder.LastExecution!.Backend);
        Bits(bars.Select(b => b.Close).ToArray(), run[indicator].ToArray());
    }

    [SkippableTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task DeviceCloseExtremaPreserveWorkspaceIsolationAndFailureRecovery(bool candle)
    {
        RequireGpu();
        var bars = Fixture(513);
        var first = Create(7, false, candle);
        var second = Create(11, true, candle);
        var a = Build(bars, first, IndicatorHistoryMode.LatestOnly).ConfigureExecution(IndicatorExecutionBackend.Gpu);
        var b = Build(bars, second, IndicatorHistoryMode.Full).ConfigureExecution(IndicatorExecutionBackend.Gpu);
        var runs = await Task.WhenAll(Task.Run(() => a.BuildAsync()), Task.Run(() => b.BuildAsync()));
        using var left = runs[0]; using var right = runs[1];
        var saved = left[first].ToArray();
        Bits(Reference(bars, 7, false, candle).ToArray(), saved);
        Bits(Reference(bars, 11, true, candle).ToArray(), right[second].ToArray());
        var original = bars[256];
        for (int field = 0; field < 5; field++)
        {
            bars[256] = new Bar(default, field == 0 ? double.NaN : 0, field == 1 ? double.NaN : 0,
                field == 2 ? double.NaN : 0, field == 3 ? double.NaN : 0, field == 4 ? double.NaN : 1);
            var expected = await Record.ExceptionAsync(() => a.ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync());
            var actual = await Record.ExceptionAsync(() => a.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
            Assert.NotNull(expected); Assert.NotNull(actual);
            Assert.Equal(expected.GetType(), actual.GetType()); Assert.Equal(expected.Message, actual.Message);
            Assert.Null(a.LastExecution);
        }
        bars[256] = original;
        using var recovered = await a.BuildAsync();
        Bits(saved, recovered[first].ToArray()); Bits(saved, left[first].ToArray());
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => a.BuildAsync(cancellation.Token));
        Assert.Null(a.LastExecution);
    }

    [Fact]
    public async Task EmptySourcedAndMixedRequiredGpuRemainUnsupported()
    {
        var indicator = new DecayingWindowExtreme(3);
        var builder = Build([], indicator, IndicatorHistoryMode.LatestOnly).ConfigureExecution(IndicatorExecutionBackend.Gpu);
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync());
        builder.ConfigureSource(Bars.From(Fixture(41))).ConfigureIndicators(indicator, new HighestHigh(3));
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync());
        builder.ConfigureIndicators(new DecayingWindowExtreme(3, true, .5));
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync());
        builder.ConfigureIndicators(indicator.Of(new Sma(3)));
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync());
        Assert.Null(builder.LastExecution);
    }

    [Fact]
    public async Task CandleExtremaRejectNonpositivePeriodsAndSourcedOrEmptyRequiredGpu()
    {
        foreach (int period in new[] { -1, 0, 3 })
        foreach (bool maximum in new[] { false, true })
        {
            var indicator = Create(period, maximum, true);
            Assert.Equal(period > 0, ValuesBarExecution.SupportsGpu([indicator]));
            var builder = Build([], indicator, IndicatorHistoryMode.Full).ConfigureExecution(IndicatorExecutionBackend.Gpu);
            await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync());
            if (period <= 0)
                await Assert.ThrowsAsync<NotSupportedException>(() => builder.ConfigureSource(Bars.From(Fixture(41))).BuildAsync());
        }
        var sourced = new HighestHigh(3).Of(new Sma(3));
        Assert.False(ValuesBarExecution.SupportsGpu([sourced]));
    }

    private static IIndicator Create(int period, bool maximum, bool candle) => candle
        ? maximum ? new HighestHigh(period) : new LowestLow(period) : new DecayingWindowExtreme(period, maximum);

    private static double[] Reference(IReadOnlyList<Bar> bars, int period, bool maximum, bool candle = false)
    {
        var result = new double[bars.Count];
        for (int i = 0; i < result.Length; i++)
        {
            // Scan direction independently selects newest close ties or oldest candle ties.
            double Read(int at) => candle ? maximum ? bars[at].High : bars[at].Low : bars[at].Close;
            int start = Math.Max(0, i - period + 1);
            double best = Read(candle ? start : i);
            for (int j = 1; j <= i - start; j++)
            {
                double next = Read(candle ? start + j : i - j);
                if (maximum ? next > best : next < best) best = next;
            }
            result[i] = best;
        }
        return result;
    }

    private static Bar[] Fixture(int count)
    {
        double[] values = [-0d, 0d, double.Epsilon, -double.Epsilon, double.MaxValue, -double.MaxValue,
            Math.ScaleB(1, -1022), -Math.ScaleB(1, -1022), .125, -.75, 1, Math.BitIncrement(1d)];
        return Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0,
            values[i % values.Length], values[(i + 3) % values.Length], values[(i + 5) % values.Length], 1)).ToArray();
    }
    private static StockIndicatorBuilder Build(Bar[] bars, IIndicator indicator, IndicatorHistoryMode history) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).ConfigureHistory(history);
    private static void Bits(double[] expected, double[] actual) => Assert.Equal(
        expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
}

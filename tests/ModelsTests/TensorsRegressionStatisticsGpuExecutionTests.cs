using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class TensorsRegressionStatisticsGpuExecutionTests
{
    [SkippableTheory]
    [InlineData(2)] [InlineData(3)] [InlineData(20)] [InlineData(256)]
    public async Task EveryColumnMatchesExactCpuAcrossStartupAndFinalWindows(int period)
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        foreach (int count in new[] { 1, 2, 19, 20, 21, 257, 513 })
        {
            var bars = Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i),
                i, i + 1, i - 1, i % 127 < 67 ? 3 : (i * 13 % 101) / 8d, i + 2)).ToArray();
            await Check(bars, period);
        }
    }

    [SkippableTheory]
    [InlineData(2)] [InlineData(3)] [InlineData(20)] [InlineData(256)]
    public async Task IntegerMomentBoundsAndSquaredMidpointsRemainExact(int period)
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        long limit = (1L << 20) / period;
        var random = new Random(8261);
        var bars = Enumerable.Range(0, 513).Select(i => new Bar(default, 0, 0, 0,
            i % 3 == 0 ? limit - i % 5 : random.Next(-(int)limit, (int)limit), 0)).ToArray();
        await Check(bars, period);
        foreach (var indicator in Family(period))
        {
            bars[0] = new(default, 0, 0, 0, limit + 1, 0);
            await Assert.ThrowsAsync<NotSupportedException>(() => Build(bars, indicator).BuildAsync());
        }
    }

    [SkippableTheory]
    [InlineData(-1074)] [InlineData(-1022)] [InlineData(-500)]
    [InlineData(0)] [InlineData(500)] [InlineData(990)]
    public async Task FinalGridRoundingPreservesSubnormalAndLargeOutputs(int grid)
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var bars = Enumerable.Range(0, 513).Select(i =>
            new Bar(default, 0, 0, 0, Math.ScaleB(i % 17 - 8, grid), 0)).ToArray();
        await Check(bars, 20);
    }

    [SkippableFact]
    public async Task RootMidpointTiesAndNegativeUnderflowZeroRoundOnce()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var bars = new[] { 0d, double.Epsilon, 0d, 3 * double.Epsilon }
            .Select(v => new Bar(default, 0, 0, 0, v, 0)).ToArray();
        await Check(bars, 2);
        var indicator = new WindowRegressionStatistics(2);
        using var run = await Build(bars, indicator).BuildAsync();
        Assert.Equal(0d, run[indicator.StandardDeviation][1]);
        Assert.Equal(2 * double.Epsilon, run[indicator.StandardDeviation][3]);
        var slope = new WindowRegressionStatistics(3);
        bars = [new(default, 0, 0, 0, double.Epsilon, 0), new(default, 0, 0, 0, 0, 0), new(default, 0, 0, 0, 0, 0)];
        using var tiny = await Build(bars, slope).BuildAsync();
        Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(tiny[slope.Slope][2]));
    }

    [SkippableFact]
    public async Task InputsPrecedeSlotMajorOverflowAndCancellationRecovers()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        foreach (var indicator in Family(2))
        {
            var bars = Enumerable.Repeat(new Bar(default, 0, 0, 0, 0, 0), 513).ToArray();
            double h = Math.ScaleB(1, 1023), a = Math.ScaleB(3, 1021);
            bars[0] = new(default, 0, 0, 0, a, 0); bars[1] = new(default, 0, 0, 0, -a, 0);
            bars[^2] = new(default, 0, 0, 0, -h, 0); bars[^1] = new(default, 0, 0, 0, h, double.NaN);
            var builder = Build(bars, indicator);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
            bars[^1] = new(default, 0, 0, 0, h, 0);
            var error = await Assert.ThrowsAsync<IndicatorOutputException>(() => builder.BuildAsync());
            Assert.Equal(0, error.OutputSlot); Assert.Equal(512, error.BarIndex);
            bars[^2] = bars[^1] = new(default, 0, 0, 0, 0, 0);
            error = await Assert.ThrowsAsync<IndicatorOutputException>(() => builder.BuildAsync());
            Assert.Equal(1, error.OutputSlot); Assert.Equal(1, error.BarIndex);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
            Array.Fill(bars, new Bar(default, 0, 0, 0, 3, 0));
            using var recovered = await builder.BuildAsync();
            Assert.All(recovered[indicator.Outputs[0]].ToArray(), x => Assert.Equal(0d, x));
        }
    }

    [SkippableFact]
    public async Task HistoricalOverlayDoesNotPublishEarlierOverflowingEndpoints()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var bars = Enumerable.Range(0, 513).Select(i =>
            new Bar(default, 0, 0, 0, i < 10 ? 0 : Math.ScaleB(7, 1021), 0)).ToArray();
        var indicator = new RegressionWithFinalLine(20);
        using var expected = await Build(bars, indicator).ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
        using var actual = await Build(bars, indicator).BuildAsync();
        foreach (var output in indicator.Outputs)
            Assert.Equal(expected[output].ToArray().Select(BitConverter.DoubleToInt64Bits),
                actual[output].ToArray().Select(BitConverter.DoubleToInt64Bits));
    }

    [SkippableFact]
    public async Task UnsupportedPeriodsGridsAndEmptySourcesRejectWhileAutoStaysCpu()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        foreach (int period in new[] { 257, int.MaxValue })
        foreach (var indicator in Family(period))
        {
            var bars = new[] { new Bar(default, 0, 0, 0, 1, double.NaN) };
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Build(bars, indicator).BuildAsync());
            bars[0] = new(default, 0, 0, 0, 1, 0);
            await Assert.ThrowsAsync<NotSupportedException>(() => Build(bars, indicator).BuildAsync());
        }
        foreach (var indicator in Family(20))
        {
            var bars = new[] { new Bar(default, 0, 0, 0, .1, 0), new Bar(default, 0, 0, 0, Math.PI, 0) };
            var required = Build(bars, indicator);
            await Assert.ThrowsAsync<NotSupportedException>(() => required.BuildAsync());
            Assert.Null(required.LastExecution);
            await Assert.ThrowsAsync<NotSupportedException>(() => Build([], indicator).BuildAsync());
            var automatic = Build(bars, indicator).ConfigureExecution(IndicatorExecutionBackend.Auto);
            using var run = await automatic.BuildAsync();
            Assert.Equal(IndicatorExecutionBackend.Cpu, automatic.LastExecution!.Backend);
        }
    }

    private static IEnumerable<IIndicator> Family(int period) =>
        new IIndicator[] { new WindowRegressionStatistics(period), new RegressionWithFinalLine(period) };

    private static async Task Check(Bar[] bars, int period)
    {
        foreach (var indicator in Family(period))
        {
            using var expected = await Build(bars, indicator).ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                var input = bars.ToArray();
                var builder = Build(input, indicator).ConfigureHistory(history);
                using var run = await builder.BuildAsync();
                Assert.Equal(IndicatorExecutionBackend.Gpu, builder.LastExecution!.Backend);
                Array.Clear(input); run.Dispose();
                foreach (var output in indicator.Outputs)
                    Assert.Equal(expected[output].ToArray().Select(BitConverter.DoubleToInt64Bits),
                        run[output].ToArray().Select(BitConverter.DoubleToInt64Bits));
                Assert.Equal(bars[^1], run.Latest.Bar);
                if (history == IndicatorHistoryMode.Full)
                {
                    int i = 0;
                    await foreach (var snapshot in run) Assert.Equal(bars[i++], snapshot.Bar);
                    Assert.Equal(bars.Length, i);
                }
            }
        }
    }

    private static StockIndicatorBuilder Build(Bar[] bars, IIndicator indicator) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator)
            .ConfigureExecution(IndicatorExecutionBackend.Gpu);
}

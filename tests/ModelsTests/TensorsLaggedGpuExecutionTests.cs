using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class TensorsLaggedGpuExecutionTests
{
    private static readonly PriceChangeKind[] Kinds = [PriceChangeKind.Difference, PriceChangeKind.Gain, PriceChangeKind.Loss, PriceChangeKind.Ratio];
    private static void RequireGpu() => Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);

    [SkippableFact]
    public async Task LaggedDeviceKernelsPreserveStartupPeriodsBitsAndOwnedHistory()
    {
        RequireGpu();
        foreach (var kind in Kinds)
        foreach (int count in new[] { 1, 2, 1023, 1024, 1025 })
        foreach (int period in new[] { 1, 3, 1024, int.MaxValue })
        {
            var bars = Enumerable.Range(0, count).Select(i => new Bar(default, 1, 2, 0, 1 + (i % 37) / 8d, 1)).ToArray();
            await Compare(bars, new LaggedPriceChange(period, kind), snapshots: count == 1025);
        }
        double[] values = [-double.MaxValue / 4, -1, -double.Epsilon, -0d, 0d, double.Epsilon, 1, double.MaxValue / 4];
        foreach (var kind in Kinds)
        {
            var bars = new List<Bar>();
            foreach (double previous in values) foreach (double current in values)
            {
                if (!double.IsFinite(LaggedPriceChange.Calculate(current, previous, kind))) continue;
                foreach (double price in new[] { 0, previous, current, 0 }) bars.Add(new Bar(default, 0, 1, -1, price, 1));
            }
            await Compare(bars.ToArray(), new LaggedPriceChange(1, kind));
        }
    }

    [SkippableFact]
    public async Task LaggedDeviceKernelsPreserveFailuresCancellationAndConcurrentArguments()
    {
        RequireGpu();
        var bars = Enumerable.Range(0, 1025).Select(i => new Bar(default, 0, 2, 0, 1 + i % 7, 1)).ToArray();
        await Task.WhenAll(Enumerable.Range(0, 8).Select(i => Task.Run(() => Compare(bars,
            new LaggedPriceChange(i + 1, Kinds[i % Kinds.Length])))));
        foreach (var kind in Kinds)
        {
            var overflow = bars.ToArray();
            overflow[0] = new Bar(default, 0, 1, 0, kind == PriceChangeKind.Ratio ? double.Epsilon : -double.MaxValue, 1);
            overflow[1] = new Bar(default, 0, 1, 0, double.MaxValue, 1);
            overflow[2] = new Bar(default, 0, 1, 0, -double.MaxValue, 1);
            await Compare(overflow, new LaggedPriceChange(1, kind));
            foreach (int field in Enumerable.Range(0, 5))
            {
                var invalid = overflow.ToArray();
                invalid[^1] = new Bar(default, field == 0 ? double.NaN : 0, field == 1 ? double.NaN : 1,
                    field == 2 ? double.NaN : 0, field == 3 ? double.NaN : 1, field == 4 ? double.NaN : 1);
                await Compare(invalid, new LaggedPriceChange(1, kind));
            }
            var builder = Builder(bars, new LaggedPriceChange(3, kind), IndicatorHistoryMode.Full, IndicatorExecutionBackend.Gpu);
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancellation.Token));
            Assert.Null(builder.LastExecution);
        }
    }

    [Fact]
    public async Task UnsupportedLaggedFormsGraphsAndEmptySourcesRejectRequiredGpu()
    {
        var bars = new[] { new Bar(default, 0, 2, 0, 1, 1) };
        foreach (var kind in new[] { PriceChangeKind.Fraction, PriceChangeKind.Percent, PriceChangeKind.RatioPercent })
            await Assert.ThrowsAsync<NotSupportedException>(() => Builder(bars, new LaggedPriceChange(3, kind), IndicatorHistoryMode.Full, IndicatorExecutionBackend.Gpu).BuildAsync());
        foreach (var kind in Kinds)
        {
            var indicator = new LaggedPriceChange(3, kind);
            await Assert.ThrowsAsync<NotSupportedException>(() => Builder([], indicator, IndicatorHistoryMode.Full, IndicatorExecutionBackend.Gpu).BuildAsync());
            await Assert.ThrowsAsync<NotSupportedException>(() => Builder(bars, indicator.Of(new Sma(3)), IndicatorHistoryMode.Full, IndicatorExecutionBackend.Gpu).BuildAsync());
            await Assert.ThrowsAsync<NotSupportedException>(() => Builder(bars, new LaggedPriceChange(3, kind), IndicatorHistoryMode.Full, IndicatorExecutionBackend.Gpu)
                .ConfigureIndicators(new LaggedPriceChange(3, kind), new HighestHigh(3)).BuildAsync());
        }
    }
    private static StockIndicatorBuilder Builder(Bar[] bars, IIndicator indicator, IndicatorHistoryMode history, IndicatorExecutionBackend backend) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).ConfigureHistory(history).ConfigureExecution(backend);
    private static async Task Compare(Bar[] bars, IIndicator indicator, bool snapshots = false)
    {
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            IIndicatorRun? cpu = null, gpu = null;
            var builder = Builder(bars, indicator, history, IndicatorExecutionBackend.Gpu);
            var expectedError = await Record.ExceptionAsync(async () => cpu = await Builder(bars, indicator, history, IndicatorExecutionBackend.Cpu).BuildAsync());
            var actualError = await Record.ExceptionAsync(async () => gpu = await builder.BuildAsync());
            using (cpu) using (gpu)
            {
                if (expectedError is not null)
                {
                    Assert.NotNull(actualError); Assert.Equal(expectedError.GetType(), actualError.GetType()); Assert.Equal(expectedError.Message, actualError.Message);
                    Assert.Null(builder.LastExecution);
                    continue;
                }
                Assert.Null(actualError); Assert.Equal(IndicatorExecutionBackend.Gpu, builder.LastExecution!.Backend);
                Assert.False(string.IsNullOrWhiteSpace(builder.LastExecution.DeviceName));
                var expected = cpu![indicator].ToArray();
                Bits(expected, gpu![indicator].ToArray()); Assert.Equal(cpu.Latest.IsWarmedUp, gpu.Latest.IsWarmedUp);
                if (snapshots && history == IndicatorHistoryMode.Full)
                {
                    var original = bars.ToArray(); Array.Clear(bars);
                    using var later = await builder.BuildAsync();
                    Bits(expected, gpu[indicator].ToArray()); int index = 0;
                    await foreach (var snapshot in gpu) Assert.Equal(original[index++], snapshot.Bar);
                    original.CopyTo(bars, 0);
                }
            }
        }
    }
    private static void Bits(double[] expected, double[] actual) => Assert.Equal(
        expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
}

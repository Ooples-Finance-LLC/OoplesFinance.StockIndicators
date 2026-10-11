using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class TensorsCandlePolarityGpuExecutionTests
{
    [SkippableFact]
    public async Task DeviceComparisonsPreserveAllEndpointsAndOwnedSnapshots()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        double[] endpoints = [-double.MaxValue, -1, -double.Epsilon, -0d, 0d, double.Epsilon, 1, double.MaxValue];
        var cases = (from open in endpoints from close in endpoints select new Bar(default, open, 1, -1, close, 1)).ToArray();
        foreach (bool bullish in new[] { false, true })
        foreach (int count in new[] { 1, 2, cases.Length, 1023, 1024, 1025, 8193 })
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var bars = Enumerable.Range(0, count).Select(i => cases[i % cases.Length]).ToArray();
            var original = bars.ToArray();
            var expected = bars.Select(b => (bullish ? b.Close > b.Open : b.Close < b.Open) ? 1d : 0d).ToArray();
            IIndicator indicator = bullish ? new BullishCandle() : new BearishCandle();
            var builder = Build(bars, indicator, history);
            using var gpu = await builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync();
            Assert.Equal(IndicatorExecutionBackend.Gpu, builder.LastExecution!.Backend);
            Assert.False(string.IsNullOrWhiteSpace(builder.LastExecution.DeviceName));
            Bits(expected, gpu[indicator].ToArray());
            using var cpu = await builder.ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
            Bits(expected, cpu[indicator].ToArray());
            Assert.Equal(cpu.Latest.IsWarmedUp, gpu.Latest.IsWarmedUp);
            Array.Clear(bars);
            using var next = await builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync();
            Bits(expected, gpu[indicator].ToArray());
            Assert.Equal(original[^1], gpu.Latest.Bar);
            if (history == IndicatorHistoryMode.Full)
            {
                int index = 0;
                await foreach (var snapshot in gpu) Assert.Equal(original[index++], snapshot.Bar);
                Assert.Equal(count, index);
            }
        }
    }

    [SkippableFact]
    public async Task DeviceComparisonsPreserveWorkspaceIsolationValidationAndCancellation()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var bars = Enumerable.Range(0, 1025).Select(i => new Bar(default, i % 3, 10, -10, i % 5, 1)).ToArray();
        await Task.WhenAll(Enumerable.Range(0, 8).Select(n => Task.Run(async () =>
        {
            IIndicator indicator = (n % 4) switch
            {
                0 => new BullishCandle(), 1 => new BearishCandle(),
                2 => new EngulfingPattern(), _ => new CandleArithmetic(CandleArithmeticOperation.Add)
            };
            var builder = Build(bars, indicator, IndicatorHistoryMode.LatestOnly);
            using var cpu = await builder.ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
            using var gpu = await builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync();
            Bits(cpu[indicator].ToArray(), gpu[indicator].ToArray());
        })));
        foreach (var indicator in new IIndicator[] { new BullishCandle(), new BearishCandle() })
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            foreach (int field in Enumerable.Range(0, 5))
            {
                var invalid = bars.ToArray();
                invalid[1023] = new Bar(default, field == 0 ? double.NaN : 0, field == 1 ? double.NaN : 1,
                    field == 2 ? double.NaN : 0, field == 3 ? double.NaN : 1, field == 4 ? double.NaN : 1);
                var builder = Build(invalid, indicator, history);
                var expected = await Record.ExceptionAsync(() => builder.ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync());
                var actual = await Record.ExceptionAsync(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
                Assert.NotNull(expected); Assert.NotNull(actual);
                Assert.Equal(expected.GetType(), actual.GetType()); Assert.Equal(expected.Message, actual.Message);
                Assert.Null(builder.LastExecution);
            }
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            var cancelled = Build(bars, indicator, history).ConfigureExecution(IndicatorExecutionBackend.Gpu);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled.BuildAsync(cancellation.Token));
            Assert.Null(cancelled.LastExecution);
        }
    }

    [Fact]
    public async Task EmptySourcedAndMixedRequiredGpuComparisonsRemainUnsupported()
    {
        foreach (var indicator in new IndicatorBase[] { new BullishCandle(), new BearishCandle() })
        {
            var builder = Build([], indicator, IndicatorHistoryMode.LatestOnly).ConfigureExecution(IndicatorExecutionBackend.Gpu);
            await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync());
            Assert.Null(builder.LastExecution);
            builder.ConfigureSource(Bars.From(new[] { new Bar(default, 1, 2, 0, 1, 1) }));
            builder.ConfigureIndicators(indicator, new HighestHigh(3));
            await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync());
            Assert.Null(builder.LastExecution);
            builder.ConfigureIndicators(indicator.Of(new Sma(3)));
            await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync());
            Assert.Null(builder.LastExecution);
        }
    }

    private static StockIndicatorBuilder Build(Bar[] bars, IIndicator indicator, IndicatorHistoryMode history) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).ConfigureHistory(history);
    private static void Bits(double[] expected, double[] actual) => Assert.Equal(
        expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
}

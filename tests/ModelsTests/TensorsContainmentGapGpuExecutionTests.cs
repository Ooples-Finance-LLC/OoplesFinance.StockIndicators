using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class TensorsContainmentGapGpuExecutionTests
{
    [SkippableFact]
    public async Task DeviceWindowsPreserveReferencesEndpointsAndOwnedSnapshots()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        foreach (int operation in Enumerable.Range(0, 5))
        foreach (bool shadows in new[] { false, true })
        foreach (int count in new[] { 1, 2, 5, 1023, 1024, 1025, 8193 })
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var bars = Fixture(count);
            var original = bars.ToArray();
            var indicator = Create(operation, 3, shadows);
            var expected = Reference(indicator, bars);
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
    public async Task DeviceWindowsPreservePeriodBoundsAndWidePrices()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        double[] points = [-double.MaxValue, -1, -double.Epsilon, -0d, 0d, double.Epsilon, 1, double.MaxValue];
        var wide = (from open in points from close in points
            select new Bar(default, open, double.MaxValue, -double.MaxValue, close, 1)).ToArray();
        foreach (int operation in Enumerable.Range(0, 5))
        foreach (bool shadows in new[] { false, true })
        foreach (int period in new[] { 1, 3, 17, 4095, int.MaxValue })
        foreach (var bars in new[] { Fixture(129), wide })
        {
            var indicator = Create(operation, period, shadows);
            var expected = Reference(indicator, bars);
            var builder = Build(bars, indicator, IndicatorHistoryMode.LatestOnly);
            using var gpu = await builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync();
            Assert.Equal(IndicatorExecutionBackend.Gpu, builder.LastExecution!.Backend);
            Bits(expected, gpu[indicator].ToArray());
        }
        foreach (int operation in Enumerable.Range(0, 5))
        {
            var values = Reference(Create(operation, 3, false), Fixture(1025));
            Assert.Contains(1d, values); Assert.Contains(0d, values);
        }
    }

    [SkippableFact]
    public async Task DeviceComparisonsPreserveWorkspaceIsolationValidationAndCancellation()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var bars = Enumerable.Range(0, 1025).Select(i => new Bar(default, i % 3, 10, -10, i % 5, 1)).ToArray();
        await Task.WhenAll(Enumerable.Range(0, 8).Select(n => Task.Run(async () =>
        {
            IIndicator indicator = n < 5 ? Create(n, n + 1, n % 2 == 0) : n switch
            {
                5 => new Sma(3), 6 => new EngulfingPattern(),
                _ => new CandleArithmetic(CandleArithmeticOperation.Add)
            };
            var builder = Build(bars, indicator, IndicatorHistoryMode.LatestOnly);
            using var cpu = await builder.ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
            using var gpu = await builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync();
            Bits(cpu[indicator].ToArray(), gpu[indicator].ToArray());
        })));
        foreach (var indicator in Enumerable.Range(0, 5).Select(n => Create(n, 3, true)))
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
        foreach (var indicator in Enumerable.Range(0, 5).Select(n => Create(n, 3, true)))
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

    private static IndicatorBase Create(int kind, int period, bool shadows = false) => kind switch
    {
        0 => new StrictHaramiCandle(shadows),
        1 => new BullishHaramiPattern(period, shadows),
        2 => new BearishHaramiPattern(period, shadows),
        3 => new UpsideTasukiGapPattern(period),
        4 => new DownsideTasukiGapPattern(period),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
    private static Bar[] Fixture(int count)
    {
        var patterns = new List<Bar>();
        foreach (bool mirror in new[] { false, true })
        {
            for (int i = 0; i <= 20; i++)
                Add(new Bar(default, 60 - 2 * i, 80 + 2 * i, -i, 54 - 2 * i, 1), mirror);
            Add(new Bar(default, 15, 30, -4, 19, 1), mirror);
            for (int i = 20; i >= 0; i--)
                Add(new Bar(default, 10 - 2 * i, 14 - 2 * i, 9 - 2 * i, 13 - 2 * i, 1), mirror);
            Add(new Bar(default, 17, 22, 16, 21, 1), mirror);
            Add(new Bar(default, 19, 20, 14, 15, 1), mirror);
        }
        return Enumerable.Range(0, count).Select(i => patterns[i % patterns.Count]).ToArray();
        void Add(Bar bar, bool mirror) => patterns.Add(mirror
            ? new Bar(default, -bar.Open, -bar.Low, -bar.High, -bar.Close, 1) : bar);
    }
    private static double[] Reference(IndicatorBase indicator, Bar[] bars) => (indicator switch
    {
        StrictHaramiCandle c => StrictHaramiReference.Evaluate(bars, 0, 1, c.ContainedShadows),
        BullishHaramiPattern c => StrictHaramiReference.Evaluate(bars, 1, c.Period, c.ContainedShadows),
        BearishHaramiPattern c => StrictHaramiReference.Evaluate(bars, -1, c.Period, c.ContainedShadows),
        UpsideTasukiGapPattern c => TrendTasukiReference.Evaluate(bars, true, c.Period),
        DownsideTasukiGapPattern c => TrendTasukiReference.Evaluate(bars, false, c.Period),
        _ => throw new ArgumentOutOfRangeException(nameof(indicator))
    }).ToArray();

    private static StockIndicatorBuilder Build(Bar[] bars, IIndicator indicator, IndicatorHistoryMode history) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).ConfigureHistory(history);
    private static void Bits(double[] expected, double[] actual) => Assert.Equal(
        expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
}

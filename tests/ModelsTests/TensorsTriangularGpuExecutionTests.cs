using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class TensorsTriangularGpuExecutionTests
{
    [SkippableTheory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(20)]
    [InlineData(65)]
    [InlineData(4096)]
    [InlineData(65535)]
    public async Task DeviceRecurrenceMatchesExactCpuAcrossTiles(int period)
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        foreach (int count in new[] { 1, 63, 64, 65, 513, 8193 })
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var bars = Enumerable.Range(0, count).Select(i => new Bar(default, 0, 1, -1,
                i % 7 == 0 ? -0d : (i % 31 - 15) / 8d, 1)).ToArray();
            if (period == 1) bars[0] = new Bar(default, 0, 1, -1, double.Epsilon, 1);
            var indicator = new TriangularWindowAverage(period);
            using var cpu = await Builder(bars, indicator, mode).ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
            var builder = Builder(bars, indicator, mode); using var gpu = await builder.BuildAsync();
            Assert.Equal(IndicatorExecutionBackend.Gpu, builder.LastExecution!.Backend);
            Assert.Equal(cpu[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits),
                gpu[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits));
        }
    }

    [SkippableFact]
    public async Task CachedWeightedModesRejectionAndRecoveryRemainIsolated()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var bars = Enumerable.Range(0, 513).Select(i => new Bar(default, 0, 1, -1, i % 31 / 8d, 1)).ToArray();
        foreach (IIndicator indicator in new IIndicator[] { new Wma(20), new TriangularWindowAverage(20), new FixedPeriodWma(20), new TriangularWindowAverage(3) })
        {
            using var cpu = await Builder(bars, indicator).ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
            using var gpu = await Builder(bars, indicator).BuildAsync();
            Assert.Equal(cpu[indicator].ToArray(), gpu[indicator].ToArray());
        }
        var triangular = new TriangularWindowAverage(20);
        var builder = Builder(bars, triangular); using var saved = await builder.BuildAsync();
        foreach (double value in new[] { double.Epsilon, double.MaxValue, .1 })
        {
            bars[0] = new Bar(default, 0, 1, -1, value, 1);
            await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync());
            Assert.Null(builder.LastExecution);
        }
        bars[^1] = new Bar(default, 0, 1, -1, 0, double.NaN);
        await Assert.ThrowsAnyAsync<ArgumentException>(() => builder.BuildAsync());
        bars[0] = new Bar(default, 0, 1, -1, 0, 1);
        bars[^1] = new Bar(default, 0, 1, -1, 512 % 31 / 8d, 1);
        using var recovered = await builder.BuildAsync();
        Assert.Equal(saved[triangular].ToArray(), recovered[triangular].ToArray());
        foreach (int period in new[] { 65536, int.MaxValue })
            await Assert.ThrowsAsync<NotSupportedException>(() => Builder(bars, new TriangularWindowAverage(period)).BuildAsync());
    }

    private static StockIndicatorBuilder Builder(Bar[] bars, IIndicator indicator, IndicatorHistoryMode mode = IndicatorHistoryMode.Full) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator)
            .ConfigureHistory(mode).ConfigureExecution(IndicatorExecutionBackend.Gpu);
}

using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedPriceRangeTests
{
    [Fact]
    public void SubtractionMatchesExactAccumulatorIncludingSignedZerosAndWideExponents()
    {
        double[] edge = [-0d, 0d, double.Epsilon, -double.Epsilon, double.MaxValue, -double.MaxValue, 1, -1];
        var random = new Random(419);
        var values = edge.Concat(Enumerable.Range(0, 500).Select(_ =>
            BitConverter.Int64BitsToDouble(random.NextInt64()))).Where(double.IsFinite).ToArray();
        foreach (double high in values)
        foreach (double low in edge)
        {
            var exact = new ExactMeanAccumulator(); exact.Add(high); exact.Add(low, -1);
            var state = new WindowPriceRange.State(1);
            var bar = new Bar(default, 0, high, low, 0, 1);
            Assert.Equal(BitConverter.DoubleToInt64Bits(exact.Mean(1)), BitConverter.DoubleToInt64Bits(state.Update(in bar)));
        }
    }

    [SkippableTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task CpuAndGpuPreserveScanOwnershipOverflowAndInputFailure(bool gpu)
    {
        if (gpu) Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        foreach (int count in new[] { 511, 512, 513, 1025 })
        foreach (int period in new[] { 1, 3, 20, int.MaxValue })
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var bars = Enumerable.Range(0, count).Select(i => new Bar(default, 0, (i % 31 - 15) / 8d, (i % 23 - 12) / 8d, 0, 1)).ToArray();
            var originalBars = bars.ToArray();
            var indicator = new WindowPriceRange(period);
            var builder = Build(bars, indicator, gpu).ConfigureHistory(history);
            using var saved = await builder.BuildAsync();
            Assert.Equal(gpu ? IndicatorExecutionBackend.Gpu : IndicatorExecutionBackend.Cpu, builder.LastExecution!.Backend);
            var expected = bars.Select((_, i) =>
            {
                var window = bars.Skip(Math.Max(0, i - period + 1)).Take(Math.Min(period, i + 1));
                var exact = new ExactMeanAccumulator(); exact.Add(window.Max(b => b.High)); exact.Add(window.Min(b => b.Low), -1);
                return exact.Mean(1);
            }).ToArray();
            Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), saved[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits));
            var original = bars[0];
            bars[0] = new Bar(default, 0, double.MaxValue, -double.MaxValue, 0, 1);
            await SameFailure(bars, indicator, builder);
            bars[^1] = new Bar(default, 0, 1, 0, 0, double.NaN);
            await SameFailure(bars, indicator, builder);
            bars[0] = original; bars[^1] = new Bar(default, 0, 0, 0, 0, 1);
            using var recovered = await builder.BuildAsync();
            Assert.Equal(expected, saved[indicator].ToArray());
            Assert.Equal(originalBars[^1], saved.Latest.Bar);
            if (history == IndicatorHistoryMode.Full)
            {
                int index = 0;
                await foreach (var snapshot in saved) Assert.Equal(originalBars[index++], snapshot.Bar);
                Assert.Equal(originalBars.Length, index);
            }
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancel.Token));
        }
    }

    [Fact]
    public async Task MixedBoundedRootsAndEmptyInputRetainFacadeBehavior()
    {
        var bars = Enumerable.Range(0, 8193).Select(i => new Bar(default, 0, i % 13, -(i % 7), 0, 1)).ToArray();
        IIndicator[] roots = [new WindowPriceRange(20), new WindowExtremeIndex(7)];
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots).ConfigureHistory(history);
            using var actual = await builder.BuildAsync();
            using var expected = await builder.ConfigureBehavior(_ => { }).BuildAsync();
            foreach (var root in roots) Assert.Equal(expected[root].ToArray(), actual[root].ToArray());
        }
        using var empty = await Build([], roots[0], false).BuildAsync();
        Assert.Equal(0, empty.BarCount);
        await Assert.ThrowsAsync<NotSupportedException>(() => Build([], roots[0], true).BuildAsync());
    }

    private static StockIndicatorBuilder Build(Bar[] bars, IIndicator indicator, bool gpu) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator)
        .ConfigureExecution(gpu ? IndicatorExecutionBackend.Gpu : IndicatorExecutionBackend.Cpu);

    private static async Task SameFailure(Bar[] bars, IIndicator indicator, StockIndicatorBuilder builder)
    {
        var expected = await Record.ExceptionAsync(() => Build(bars, indicator, false).ConfigureBehavior(_ => { }).BuildAsync());
        var actual = await Record.ExceptionAsync(() => builder.BuildAsync());
        Assert.NotNull(expected); Assert.NotNull(actual);
        Assert.Equal(expected.GetType(), actual.GetType()); Assert.Equal(expected.Message, actual.Message);
        Assert.Null(builder.LastExecution);
    }
}

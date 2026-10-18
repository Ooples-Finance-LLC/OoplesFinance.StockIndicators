using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class SharedHullTests
{
    [Theory]
    [InlineData(HullWindowConvention.ExpandingFloor)]
    [InlineData(HullWindowConvention.FullWindowFloor)]
    [InlineData(HullWindowConvention.FullWindowRounded)]
    public async Task BoundedReplayPreservesRoundedStagesAndPresence(HullWindowConvention convention)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            foreach (int period in new[] { 2, 5, 20, 4033, 4034, int.MaxValue })
            foreach (int count in new[] { 0, 1, 511, 512, 513, period is 4033 or 4034 ? 8193 : 1025 })
            {
                var input = Enumerable.Range(0, count).Select(i => new Bar(default, 0, 1, -1,
                    i == 0 ? double.Epsilon : i % 13 == 0 ? -0d : (i % 61 - 30) / 8d, 1)).ToArray();
                var hull = new HullWindow(period, convention);
                using var reference = await Builder(input, hull).ConfigureBehavior(_ => { }).BuildAsync();
                foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
                {
                    using var actual = await Builder(input, hull).ConfigureHistory(history).BuildAsync();
                    using var mixed = await Builder(input, hull).ConfigureIndicators(hull, new LowestLow(7))
                        .ConfigureHistory(history).BuildAsync();
                    foreach (var output in hull.Outputs)
                    {
                        Assert.Equal(Bits(reference, output), Bits(actual, output));
                        Assert.Equal(Bits(reference, output), Bits(mixed, output));
                    }
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Fact]
    public async Task OverflowInputOrderingRecoveryAndSnapshotsMatchOrdinaryBuilder()
    {
        var hull = new HullWindow(5, HullWindowConvention.ExpandingFloor);
        var bars = Enumerable.Range(0, 1025).Select(i => new Bar(default, 1, 2, 0, i % 17, 1)).ToArray();
        var builder = Builder(bars, hull);
        using var saved = await builder.BuildAsync();
        var original = bars.ToArray();
        // A large negative-to-positive jump overflows the published Hull value.
        for (int i = 500; i < 505; i++) bars[i] = new Bar(default, 0, 1, -1, -double.MaxValue, 1);
        bars[505] = new Bar(default, 0, 1, -1, double.MaxValue, 1);
        await SameFailure(bars, hull, builder);
        bars[900] = new Bar(default, 0, 1, -1, 0, double.NaN);
        await SameFailure(bars, hull, builder);
        original.CopyTo(bars, 0);
        using var recovered = await builder.BuildAsync();
        Assert.Equal(Bits(saved, hull.Value), Bits(recovered, hull.Value));
        Array.Clear(bars);
        int index = 0;
        await foreach (var snapshot in saved) Assert.Equal(original[index++], snapshot.Bar);
        Assert.Equal(original.Length, index);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancellation.Token));
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
    }

    private static async Task SameFailure(Bar[] bars, HullWindow hull, StockIndicatorBuilder builder)
    {
        var expected = await Record.ExceptionAsync(() => Builder(bars, hull).ConfigureBehavior(_ => { }).BuildAsync());
        var actual = await Record.ExceptionAsync(() => builder.BuildAsync());
        Assert.NotNull(expected); Assert.NotNull(actual);
        Assert.Equal(expected.GetType(), actual.GetType()); Assert.Equal(expected.Message, actual.Message);
        Assert.Null(builder.LastExecution);
    }
    private static long[] Bits(IIndicatorRun run, IIndicatorOutput output) => run[output].ToArray().Select(BitConverter.DoubleToInt64Bits).ToArray();
    private static StockIndicatorBuilder Builder(Bar[] bars, IIndicator indicator) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator);
}

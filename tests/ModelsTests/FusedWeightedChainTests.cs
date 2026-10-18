using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class FusedWeightedChainTests
{
    [Theory]
    [InlineData(1, 1)] [InlineData(1, 20)] [InlineData(20, 1)]
    [InlineData(2, 2)] [InlineData(3, 20)] [InlineData(20, 3)]
    [InlineData(4096, 20)] [InlineData(int.MaxValue, 1)]
    public async Task FusedStagesPreserveConfiguredSeriesAndSnapshots(int firstPeriod, int lastPeriod)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int count in new[] { 0, 1, 63, 511, 512, 513, 8193 })
            {
                if (firstPeriod == int.MaxValue && count > 63) continue;
                var bars = Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i),
                    0, 1, -1, i % 7 == 0 ? -0d : (i % 31 - 15) / 8d, 1)).ToArray();
                var first = new FixedPeriodWma(firstPeriod); var last = new FixedPeriodWma(lastPeriod); last.Of(first);
                Assert.True(ValuesBarExecution.SupportsOwned([last]));
                foreach (int workers in new[] { 1, 8 })
                foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
                {
                    CpuParallelSettings.MaxDegreeOfParallelism = workers;
                    using var expected = await Builder(bars, last, history).ConfigureBehavior(_ => { }).BuildAsync();
                    var builder = Builder(bars, last, history);
                    using var actual = await builder.BuildAsync();
                    Compare(expected, actual, last);
                    Assert.Throws<KeyNotFoundException>(() => { _ = actual[first].Length; });
                    if (count > 0)
                    {
                        Assert.Equal(expected.Latest.IsWarmedUp, actual.Latest.IsWarmedUp);
                        Assert.Equal(bars[^1], actual.Latest.Bar);
                        Assert.Contains(history == IndicatorHistoryMode.Full ? "Fused CPU values" : "CPU values execution", builder.LastExecution!.Reason);
                    }
                    if (history == IndicatorHistoryMode.Full)
                    {
                        int index = 0;
                        await foreach (var snapshot in actual) Assert.Equal(bars[index++], snapshot.Bar);
                        Assert.Equal(count, index);
                    }
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Fact]
    public async Task RoundedIntermediateFallbackOwnershipFailuresAndGraphGuards()
    {
        var first = new FixedPeriodWma(2); var last = new FixedPeriodWma(2); last.Of(first);
        var bars = Enumerable.Range(0, 1025).Select(i => new Bar(default, 0, 1, -1,
            i == 0 ? 1 : i == 1 ? 3 : (i % 31 - 15) / 8d, 1)).ToArray();
        bars[63] = new Bar(default, 0, 1, -1, double.Epsilon, 1);
        bars[512] = new Bar(default, 0, 1, -1, double.MaxValue, 1);
        bars[513] = new Bar(default, 0, 1, -1, -double.MaxValue, 1);
        using var expected = await Builder(bars, last).ConfigureBehavior(_ => { }).BuildAsync();
        var builder = Builder(bars, last);
        using var actual = await builder.BuildAsync();
        Compare(expected, actual, last);
        Assert.Throws<KeyNotFoundException>(() => { _ = actual[first].Length; });
        Assert.Equal(BitConverter.DoubleToInt64Bits(Math.BitIncrement(17d / 9)), BitConverter.DoubleToInt64Bits(actual[last][1]));
        var original = bars.ToArray();
        bars[^1] = new Bar(default, 0, 1, -1, 0, double.NaN);
        var expectedError = await Record.ExceptionAsync(() => Builder(bars, last).ConfigureBehavior(_ => { }).BuildAsync());
        var error = await Record.ExceptionAsync(() => builder.BuildAsync());
        Assert.NotNull(expectedError); Assert.NotNull(error);
        Assert.Equal(expectedError.GetType(), error.GetType()); Assert.Equal(expectedError.Message, error.Message);
        Assert.Null(builder.LastExecution);
        original.CopyTo(bars, 0); using var recovered = await builder.BuildAsync();
        Compare(actual, recovered, last);
        Array.Clear(bars); int index = 0;
        await foreach (var snapshot in actual) Assert.Equal(original[index++], snapshot.Bar);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancellation.Token));
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
        Assert.False(ValuesBarExecution.SupportsOwned([first, last]));
        var third = new FixedPeriodWma(2); third.Of(last);
        Assert.False(ValuesBarExecution.SupportsOwned([third]));
        var huge = new FixedPeriodWma(int.MaxValue); huge.Of(new FixedPeriodWma(int.MaxValue));
        Assert.False(ValuesBarExecution.SupportsOwned([huge]));
    }

    private static StockIndicatorBuilder Builder(Bar[] bars, IIndicator last, IndicatorHistoryMode history = IndicatorHistoryMode.Full) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(last).ConfigureHistory(history);
    private static void Compare(IIndicatorRun expected, IIndicatorRun actual, IIndicator indicator) => Assert.Equal(
        expected[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits), actual[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits));
}

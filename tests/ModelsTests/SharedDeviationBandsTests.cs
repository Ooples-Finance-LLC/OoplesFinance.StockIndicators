using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class SharedDeviationBandsTests
{
    [Theory]
    [InlineData(1, -2, true, 513)]
    [InlineData(20, 2, false, 511)]
    [InlineData(20, 2, false, 1025)]
    [InlineData(20, 0, true, 513)]
    [InlineData(4096, .1, false, 8201)]
    [InlineData(4097, 2, false, 8201)]
    [InlineData(int.MaxValue, 2, false, 513)]
    public async Task SeededReplayPreservesAllSlotsAndOwnedSnapshots(int period, double factor, bool percent, int count)
    {
        int prior = AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism = 8;
            var bars = Enumerable.Range(0, count).Select(i => new Bar(default, 0, 1, -1,
                i < 100 ? 0 : 10 + i % 101 / 8d, 1)).ToArray();
            var indicator = new WindowDeviationBands(period, factor, percent);
            using var expected = await Builder(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync();
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                using var actual = await Builder(bars, indicator).ConfigureHistory(history).BuildAsync();
                using var mixed = await Builder(bars, indicator).ConfigureHistory(history)
                    .ConfigureIndicators(indicator, new LowestLow(7)).BuildAsync();
                foreach (var output in indicator.Outputs)
                {
                    Assert.Equal(expected[output].ToArray().Select(BitConverter.DoubleToInt64Bits), actual[output].ToArray().Select(BitConverter.DoubleToInt64Bits));
                    Assert.Equal(expected[output].ToArray().Select(BitConverter.DoubleToInt64Bits), mixed[output].ToArray().Select(BitConverter.DoubleToInt64Bits));
                }
                if (history == IndicatorHistoryMode.Full)
                {
                    var original = bars.ToArray(); Array.Clear(bars);
                    int index = 0; await foreach (var snapshot in actual) Assert.Equal(original[index++], snapshot.Bar);
                    Assert.Equal(count, index); original.CopyTo(bars, 0);
                }
            }
        }
        finally { AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SecondaryOverflowAndLaterInputFailureRetainPublicationOrder(bool invalidInput)
    {
        var indicator = new WindowDeviationBands(2, double.MaxValue);
        var bars = Enumerable.Range(0, 1025).Select(i => new Bar(default, 0, 1, -1, i % 2 * 4d, 1)).ToArray();
        if (invalidInput) bars[^1] = new Bar(default, 0, 1, -1, 0, double.NaN);
        var expected = await Record.ExceptionAsync(() => Builder(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync());
        Assert.NotNull(expected);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var builder = Builder(bars, indicator).ConfigureHistory(history);
            var actual = await Record.ExceptionAsync(() => builder.BuildAsync());
            Assert.NotNull(actual); Assert.Equal(expected.GetType(), actual.GetType()); Assert.Equal(expected.Message, actual.Message);
            Assert.Null(builder.LastExecution);
            Array.Fill(bars, new Bar(default, 0, 1, -1, 0, 1));
            using var recovered = await builder.BuildAsync();
            Assert.All(recovered[indicator.Upper].ToArray(), value => Assert.Equal(0, value));
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancel.Token));
            await Assert.ThrowsAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
            for (int i = 0; i < bars.Length; i++) bars[i] = new Bar(default, 0, 1, -1, i % 2 * 4d, 1);
            if (invalidInput) bars[^1] = new Bar(default, 0, 1, -1, 0, double.NaN);
        }
    }
    [Fact]
    public async Task FiniteBandsStillRejectOverflowingWidth()
    {
        var indicator = new WindowDeviationBands(3);
        double[] values = [1, -1, 3 * double.Epsilon];
        var bars = Enumerable.Range(0, 1025).Select(i => new Bar(default, 0, 1, -1, values[i % 3], 1)).ToArray();
        var expected = await Record.ExceptionAsync(() => Builder(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync());
        Assert.NotNull(expected);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var actual = await Record.ExceptionAsync(() => Builder(bars, indicator).ConfigureHistory(history).BuildAsync());
            Assert.NotNull(actual); Assert.Equal(expected.GetType(), actual.GetType()); Assert.Equal(expected.Message, actual.Message);
        }
    }

    private static StockIndicatorBuilder Builder(Bar[] bars, IIndicator indicator) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator);
}

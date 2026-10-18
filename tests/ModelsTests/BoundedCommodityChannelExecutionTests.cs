using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class BoundedCommodityChannelExecutionTests
{
    private static StockIndicatorBuilder Build(Bar[] bars, IIndicator indicator) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator);

    [Theory]
    [InlineData(511, 20, false)] [InlineData(512, 20, false)] [InlineData(513, 20, false)]
    [InlineData(8193, 20, false)] [InlineData(513, 1, false)]
    [InlineData(511, 20, true)] [InlineData(512, 20, true)] [InlineData(513, 20, true)]
    [InlineData(8193, 20, true)] [InlineData(513, 1, true)]
    [InlineData(17, 1073741824, true)] [InlineData(17, int.MaxValue, true)]
    [InlineData(17, int.MaxValue, false)]
    public async Task WorkersPreserveBothDefinitionsAndOwnedResults(int count, int period, bool rolling)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            var bars = Enumerable.Range(0, count).Select(i =>
            {
                double value = (i * 13 % 101 - 50) / 8d;
                return new Bar(default, value, value + 1, value - 1, value, 1);
            }).ToArray();
            bars[count / 2] = new Bar(default, 0, double.MaxValue, double.MaxValue, double.MaxValue, 1);
            bars[count / 2 + 1] = new Bar(default, 0, -double.Epsilon, -double.Epsilon, -double.Epsilon, 1);
            for (int i = Math.Max(count / 2 + 2, count - 64); i < count; i++)
                bars[i] = new Bar(default, 1, 1, 1, 1, 1);
            foreach (bool flatZero in new[] { false, true })
            {
                var indicator = new WindowCommodityChannelIndex(period, rolling, flatZero);
                using var expected = await Build(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync();
                foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
                {
                    var input = bars.ToArray();
                    using var actual = await Build(input, indicator).ConfigureHistory(history).BuildAsync();
                    Array.Clear(input); actual.Dispose();
                    foreach (var output in indicator.Outputs)
                        Assert.Equal(expected[output].ToArray().Select(BitConverter.DoubleToInt64Bits),
                            actual[output].ToArray().Select(BitConverter.DoubleToInt64Bits));
                    Assert.Equal(bars[^1], actual.Latest.Bar);
                    if (history == IndicatorHistoryMode.Full)
                    {
                        int row = 0;
                        await foreach (var snapshot in actual) Assert.Equal(bars[row++], snapshot.Bar);
                        Assert.Equal(count, row);
                    }
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task InputErrorsAndCancellationRetainTheBuilderContract(bool rolling)
    {
        var bars = Enumerable.Repeat(new Bar(default, 1, 2, 0, 1, 1), 4097).ToArray();
        bars[511] = new Bar(default, 1, 2, 0, 1, double.NaN);
        var indicator = new WindowCommodityChannelIndex(20, rolling);
        var expected = await Record.ExceptionAsync(() => Build(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync());
        Assert.NotNull(expected);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var actual = await Record.ExceptionAsync(() => Build(bars, indicator).ConfigureHistory(history).BuildAsync());
            Assert.NotNull(actual);
            Assert.Equal(expected.GetType(), actual.GetType());
            Assert.Equal(expected.Message, actual.Message);
        }
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Build(bars, indicator).BuildAsync(cancellation.Token));
    }
}

using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class BoundedReturnBetaExecutionTests
{
    private static StockIndicatorBuilder Build(Bar[] bars, IIndicator indicator) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator);

    [Theory]
    [InlineData(511, 20)] [InlineData(512, 20)] [InlineData(513, 20)]
    [InlineData(8193, 20)] [InlineData(513, 1)] [InlineData(513, 512)]
    [InlineData(17, 4096)] [InlineData(17, int.MaxValue)]
    public async Task WorkersPreserveReturnsPresenceAndOwnedHistory(int count, int period)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            var bars = Enumerable.Range(0, count).Select(i => new Bar(default,
                8 + (i * 7 % 97) / 16d, 0, 0, 10 + (i * 13 % 101) / 8d, 1)).ToArray();
            bars[count / 2] = new Bar(default, double.MaxValue, 0, 0, double.MaxValue, 1);
            bars[count / 2 + 1] = new Bar(default, double.Epsilon, 0, 0, double.Epsilon, 1);
            bars[^1] = new Bar(default, 0, 0, 0, 0, 1);
            foreach (bool reverse in new[] { false, true })
            {
                var indicator = reverse ? new WindowReturnBeta(period, CandlePriceField.Open, CandlePriceField.Close)
                    : new WindowReturnBeta(period);
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

    [Fact]
    public async Task RawInputErrorsAndCancellationKeepExistingContracts()
    {
        var bars = Enumerable.Range(0, 513).Select(i => new Bar(default, i, 1, 0, i / 8d, 1)).ToArray();
        bars[^1] = new Bar(default, 1, 1, 0, 1, double.NaN);
        var indicator = new WindowReturnBeta(20);
        var expected = await Record.ExceptionAsync(() => Build(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync());
        var actual = await Record.ExceptionAsync(() => Build(bars, indicator).BuildAsync());
        Assert.NotNull(expected); Assert.NotNull(actual);
        Assert.Equal(expected.GetType(), actual.GetType());
        Assert.Equal(expected.Message, actual.Message);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Build(bars, indicator).BuildAsync(cancellation.Token));
    }
}

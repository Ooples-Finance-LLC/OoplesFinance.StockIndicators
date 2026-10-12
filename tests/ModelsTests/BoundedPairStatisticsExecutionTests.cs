using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class BoundedPairStatisticsExecutionTests
{
    private static StockIndicatorBuilder Build(Bar[] bars, IIndicator indicator) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator);

    [Theory]
    [InlineData(511, 20)] [InlineData(512, 20)] [InlineData(513, 20)]
    [InlineData(8193, 20)] [InlineData(513, 1)] [InlineData(513, 513)]
    [InlineData(17, 4097)] [InlineData(17, int.MaxValue)]
    public async Task WorkersPreserveAllStatisticsPresenceAndOwnedHistory(int count, int period)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            var bars = Enumerable.Range(0, count).Select(i => new Bar(default,
                8 + (i * 7 % 97) / 16d, 0, 0, 10 + (i * 13 % 101) / 8d, 1)).ToArray();
            bars[count / 2] = new Bar(default, 1e100, 0, 0, -1e100, 1);
            bars[count / 2 + 1] = new Bar(default, double.Epsilon, 0, 0, -double.Epsilon, 1);
            for (int i = Math.Max(count / 2 + 2, count - 64); i < count; i++)
                bars[i] = new Bar(default, 1, 0, 0, 1, 1);
            IIndicator[] indicators = [new WindowCorrelation(period), new WindowCorrelation(period, flatZero: true),
                new WindowPairStatistics(period, CandlePriceField.Open, CandlePriceField.Close)];
            foreach (var indicator in indicators)
            {
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
    public async Task SecondaryOverflowRetainsSlotOrderAndRawInputPrecedence()
    {
        var bars = Enumerable.Range(0, 4097).Select(i => new Bar(default, 1, 0, 0,
            i % 2 == 0 ? double.MaxValue : -double.MaxValue, 1)).ToArray();
        var indicator = new WindowPairStatistics(2);
        foreach (bool invalidInput in new[] { false, true })
        {
            if (invalidInput) bars[^1] = new Bar(default, 1, 0, 0, 1, double.NaN);
            var expected = await Record.ExceptionAsync(() => Build(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync());
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                var actual = await Record.ExceptionAsync(() => Build(bars, indicator).ConfigureHistory(history).BuildAsync());
                Assert.NotNull(expected); Assert.NotNull(actual);
                Assert.Equal(expected.GetType(), actual.GetType());
                Assert.Equal(expected.Message, actual.Message);
            }
        }
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Build(bars, indicator).BuildAsync(cancellation.Token));
    }

    [Theory]
    [InlineData(-1074)] [InlineData(-400)] [InlineData(0)] [InlineData(400)]
    public async Task CertifiedColumnsPreserveEveryOutputAcrossBinaryScales(int exponent)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            var bars = Enumerable.Range(0, 4097).Select(i => new Bar(default,
                Math.ScaleB(i % 29 - 14, exponent), 0, 0,
                Math.ScaleB(i % 31 - 15, exponent), 1)).ToArray();
            foreach (IIndicator indicator in new IIndicator[] { new WindowCorrelation(20), new WindowPairStatistics(20) })
            {
                using var expected = await Build(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync();
                foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
                {
                    using var actual = await Build(bars, indicator).ConfigureHistory(history).BuildAsync();
                    foreach (var output in indicator.Outputs)
                        Assert.Equal(expected[output].ToArray().Select(BitConverter.DoubleToInt64Bits),
                            actual[output].ToArray().Select(BitConverter.DoubleToInt64Bits));
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }
}

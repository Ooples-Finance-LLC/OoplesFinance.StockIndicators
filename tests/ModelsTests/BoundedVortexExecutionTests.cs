using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class BoundedVortexExecutionTests
{
    private static StockIndicatorBuilder Build(Bar[] bars, IIndicator indicator) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator);

    [Theory]
    [InlineData(511, 14)] [InlineData(512, 14)] [InlineData(513, 14)]
    [InlineData(4097, 14)] [InlineData(513, 1)] [InlineData(17, int.MaxValue)]
    public async Task WorkersPreserveBothRatiosPresenceAndOwnedHistory(int count, int period)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            var bars = Enumerable.Range(0, count).Select(i =>
            {
                double value = 10 + (i * 13 % 101) / 8d;
                return new Bar(default, value, value + 1, value - 1, value, 1);
            }).ToArray();
            bars[count / 2] = new Bar(default, 0, double.MaxValue, -double.MaxValue, 0, 1);
            if (period > 1) bars[count / 2 + 1] = new Bar(default, 0, double.Epsilon, -double.Epsilon, 0, 1);
            var indicator = new WindowVortex(period);
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
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(14)] [InlineData(64)] [InlineData(4096)]
    public async Task CertifiedWindowsMatchExactStateIncludingFlatAndInvertedCandles(int period)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            foreach (int count in new[] { 1, 511, 512, 513, 4097 })
            {
                var bars = Enumerable.Range(0, count).Select(i =>
                {
                    double value = (i * 13 % 101 - 50) / 8d;
                    return i % 128 < 64 ? new Bar(default, 0, -0d, 0, 0, 1)
                        : new Bar(default, value, value + (i % 7 - 3), value - (i % 5), value, 1);
                }).ToArray();
                var indicator = new WindowVortex(period);
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

    [Fact]
    public async Task SecondaryOverflowPreservesInputPrecedenceAndCancellation()
    {
        var bars = Enumerable.Repeat(new Bar(default, 0, 1, 0, 0, 1), 4097).ToArray();
        // Positive ratio stays finite; only the negative output overflows.
        bars[0] = new Bar(default, 0, double.MaxValue, 0, 0, 1);
        bars[1] = new Bar(default, 0, double.Epsilon, 0, 0, 1);
        var indicator = new WindowVortex(1);
        foreach (bool invalidInput in new[] { false, true })
        {
            if (invalidInput) bars[^1] = new Bar(default, 0, 1, 0, 0, double.NaN);
            var expected = await Record.ExceptionAsync(() => Build(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync());
            Assert.NotNull(expected);
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                var actual = await Record.ExceptionAsync(() => Build(bars, indicator).ConfigureHistory(history).BuildAsync());
                Assert.NotNull(actual);
                Assert.Equal(expected.GetType(), actual.GetType());
                Assert.Equal(expected.Message, actual.Message);
            }
        }
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Build(bars, indicator).BuildAsync(cancellation.Token));
    }
}

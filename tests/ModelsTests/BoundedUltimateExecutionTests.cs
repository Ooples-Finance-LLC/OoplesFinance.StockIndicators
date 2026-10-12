using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class BoundedUltimateExecutionTests
{
    private static StockIndicatorBuilder Build(Bar[] bars, IIndicator indicator) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator);

    [Theory]
    [InlineData(511, 7, 14, 28, false)] [InlineData(512, 7, 14, 28, true)]
    [InlineData(513, 7, 14, 28, false)] [InlineData(4097, 7, 14, 28, true)]
    [InlineData(513, 1, 1, 1, true)] [InlineData(513, 14, 7, 14, true)]
    [InlineData(17, 1, 2, int.MaxValue, false)]
    public async Task WorkersPreserveWeightedRatiosPresenceAndOwnedHistory(int count, int a, int b, int c, bool zero)
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
            if (c > 1) bars[count / 2 + 1] = new Bar(default, 0, double.Epsilon, -double.Epsilon, 0, 1);
            if (count > 64) Array.Fill(bars, new Bar(default, 1, 1, 1, 1, 1), count - 32, 32);
            var indicator = new WindowUltimateOscillator(a, b, c, zero);
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
    [InlineData(1, 2, 3, false)] [InlineData(1, 2, 3, true)]
    [InlineData(7, 14, 28, false)] [InlineData(7, 14, 28, true)]
    [InlineData(1, 1, 1, true)] [InlineData(14, 7, 14, true)]
    public async Task CertifiedIntegerRatiosPreserveWideProductsAndZeroConventions(int a, int b, int c, bool zero)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            long bound = (1L << 38) / (4L * Math.Max(a, Math.Max(b, c)));
            foreach (int scale in new[] { -900, 0, 900 })
            {
                var bars = Enumerable.Range(0, 513).Select(i =>
                {
                    double value = (i % 2 == 0 ? bound : -bound) + (i % 11);
                    return i % 128 < 64 ? new Bar(default, 0, -0d, 0, 0, 1)
                        : new Bar(default, 0, Math.ScaleB(value + (i % 7 - 3), scale),
                            Math.ScaleB(value - (i % 5), scale), Math.ScaleB(value, scale), 1);
                }).ToArray();
                var indicator = new WindowUltimateOscillator(a, b, c, zero);
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

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task OverflowPreservesInputPrecedenceAndCancellation(bool zero)
    {
        var bars = Enumerable.Repeat(new Bar(default, 0, double.Epsilon, 0, 0, 1), 4097).ToArray();
        // Finite, out-of-range close creates a nonfinite output before the bad input.
        bars[3] = new Bar(default, 0, double.Epsilon, 0, double.MaxValue, 1);
        var indicator = new WindowUltimateOscillator(1, 2, 3, zero);
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

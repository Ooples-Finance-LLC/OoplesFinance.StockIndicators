using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class CompactOnBalanceVolumeExecutionTests
{
    private static StockIndicatorBuilder Build(Bar[] bars, IIndicator indicator) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator);

    [Theory]
    [InlineData(ObvSeed.Zero, false)] [InlineData(ObvSeed.FirstVolume, false)]
    [InlineData(ObvSeed.Zero, true)] [InlineData(ObvSeed.FirstVolume, true)]
    public async Task CertifiedAndExactPrefixesPreserveColumnsAndOwnership(ObvSeed seed, bool full)
    {
        foreach (int count in new[] { 1, 17, 513 })
        foreach (int scale in new[] { -1070, -500, 0, 500 })
        {
            var bars = Enumerable.Range(0, count).Select(i =>
            {
                double close = (i % 5) - 2;
                double volume = Math.ScaleB((i % 13) - 6, scale);
                return new Bar(DateTime.UnixEpoch.AddMinutes(i), i, close + 1, close - 1, close, volume);
            }).ToArray();
            await Check(bars);
        }
        await Check([new(default, 0, 0, 0, 0, double.MaxValue), new(default, 1, 1, 1, 1, 1),
            new(default, 0, 0, 0, 0, double.MaxValue), new(default, 0, 0, 0, -0d, -0d)]);

        async Task Check(Bar[] bars)
        {
            var indicator = new SeededOnBalanceVolume(seed);
            using var expected = await Build(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync();
            var input = bars.ToArray();
            using var actual = await Build(input, indicator)
                .ConfigureHistory(full ? IndicatorHistoryMode.Full : IndicatorHistoryMode.LatestOnly).BuildAsync();
            Array.Clear(input);
            actual.Dispose();
            foreach (var output in indicator.Outputs)
                Assert.Equal(expected[output].ToArray().Select(BitConverter.DoubleToInt64Bits),
                    actual[output].ToArray().Select(BitConverter.DoubleToInt64Bits));
            Assert.Equal(bars[^1], actual.Latest.Bar);
            if (full)
            {
                int row = 0;
                await foreach (var snapshot in actual) Assert.Equal(bars[row++], snapshot.Bar);
                Assert.Equal(bars.Length, row);
            }
        }
    }

    [Fact]
    public async Task LaterInvalidInputWinsOverEarlierOverflow()
    {
        Bar[] bars = [new(default, 0, 0, 0, 0, double.MaxValue),
            new(default, 1, 1, 1, 1, double.MaxValue), new(default, double.NaN, 2, 2, 2, 1)];
        var indicator = new SeededOnBalanceVolume();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Build(bars, indicator).BuildAsync());
        await Assert.ThrowsAsync<IndicatorOutputException>(() => Build(bars[..2], indicator).BuildAsync());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Build(bars, indicator).BuildAsync(cancellation.Token));
    }
}

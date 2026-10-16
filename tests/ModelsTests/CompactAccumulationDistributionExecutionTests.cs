using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class CompactAccumulationDistributionExecutionTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CapturedFlowsPrefixesAndMeansPreserveExactOutputsAndOwnership(bool full)
    {
        foreach (int count in new[] { 1, 17, 513 })
        foreach (int scale in new[] { -1070, -500, 0, 500 })
        {
            var bars = Enumerable.Range(0, count).Select(i =>
            {
                double low = (i % 23 - 12) / 8d;
                double high = low + (i % 7 == 0 ? 0 : i % 3 + 1);
                double close = low + (i % 5 - 1) / 2d;
                return new Bar(DateTime.UnixEpoch.AddMinutes(i), 123, high, low, close,
                    Math.ScaleB(i % 13 - 6, scale));
            }).ToArray();
            await Check(bars, full);
        }
        await Check([
            new(default, 0, double.MaxValue, -double.MaxValue, double.MaxValue, 1),
            new(default, 0, 3 * double.Epsilon, 0, double.Epsilon, 1),
            new(default, 0, 0, double.Epsilon, -double.Epsilon, 1),
            new(default, 0, -1, 1, -0d, -0d),
            new(default, 0, 1, 0, .1, .1)
        ], full);
        // Exact cancellation can recover after a published cumulative infinity,
        // but that earlier output must still reject the complete run.
        Bar[] overflow = [new(default, 0, 1, 0, 1, double.MaxValue),
            new(default, 0, 1, 0, 1, double.MaxValue), new(default, 0, 1, 0, 0, double.MaxValue)];
        await SameFailure(overflow, 2);
    }

    [Fact]
    public async Task InvalidInputPrecedesMultiplierFlowAndCumulativeOverflow()
    {
        foreach (var first in new[]
        {
            new Bar(default, 0, double.Epsilon, 0, double.MaxValue, 0),
            new Bar(default, 0, 1, 0, 2, double.MaxValue),
            new Bar(default, 0, 1, 0, 1, double.MaxValue)
        })
        {
            Bar[] bars = [first, first, new(default, double.NaN, 1, 0, 1, 1)];
            await SameFailure(bars, 1);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Build(bars, new(1)).BuildAsync());
            await SameFailure(bars[..2], 1);
            await Assert.ThrowsAsync<IndicatorOutputException>(() => Build(bars[..2], new(1)).BuildAsync());
        }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Build([new(default, 0, 1, 0, .5, 1)], new()).BuildAsync(new CancellationToken(true)));
        using var empty = await Build([], new(1)).BuildAsync();
        await foreach (var _ in empty) Assert.Fail("Empty input published a row.");
    }

    private static async Task SameFailure(Bar[] bars, int period)
    {
        var indicator = new AccumulationDistributionWithAverage(period);
        var expected = await Record.ExceptionAsync(() => Build(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync());
        var actual = await Record.ExceptionAsync(() => Build(bars, indicator).BuildAsync());
        Assert.NotNull(expected); Assert.NotNull(actual);
        Assert.Equal(expected.GetType(), actual.GetType());
        Assert.Equal(expected.Message, actual.Message);
    }

    private static async Task Check(Bar[] bars, bool full)
    {
        foreach (int? period in new int?[] { null, 1, 2, 14, bars.Length, int.MaxValue })
        {
            var indicator = new AccumulationDistributionWithAverage(period);
            using var expected = await Build(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync();
            var input = bars.ToArray();
            using var actual = await Build(input, indicator)
                .ConfigureHistory(full ? IndicatorHistoryMode.Full : IndicatorHistoryMode.LatestOnly).BuildAsync();
            Array.Clear(input); actual.Dispose();
            foreach (var output in indicator.Outputs)
                Assert.Equal(expected[output].ToArray().Select(BitConverter.DoubleToInt64Bits),
                    actual[output].ToArray().Select(BitConverter.DoubleToInt64Bits));
            Assert.Equal(expected.Latest.IsWarmedUp, actual.Latest.IsWarmedUp);
            Assert.Equal(bars[^1], actual.Latest.Bar);
            if (full)
            {
                int row = 0;
                await foreach (var snapshot in actual) Assert.Equal(bars[row++], snapshot.Bar);
                Assert.Equal(bars.Length, row);
            }
        }
    }

    private static StockIndicatorBuilder Build(Bar[] bars, AccumulationDistributionWithAverage indicator) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator);
}

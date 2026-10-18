using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class SharedClassicBandsTests
{
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(20)] [InlineData(4096)] [InlineData(4097)] [InlineData(int.MaxValue)]
    public async Task BoundedSmaBandsPreserveEverySlotAndOwnership(int period)
    {
        int prior = AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism = 8;
            int count = period is 4096 or 4097 ? 8201 : 1025;
            var bars = Enumerable.Range(0, count).Select(i => new Bar(default, 0, 1, -1, 10 + i % 101 / 8d, 1)).ToArray();
            var indicator = new ClassicDeviationBands(period, .1, 3, firstPriceSeed: true, suppression: 7);
            using var expected = await Builder(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync();
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                using var actual = await Builder(bars, indicator).ConfigureHistory(history).BuildAsync();
                using var mixed = await Builder(bars, indicator).ConfigureHistory(history).ConfigureIndicators(indicator, new LowestLow(7)).BuildAsync();
                foreach (var output in indicator.Outputs)
                {
                    Assert.Equal(expected[output].ToArray().Select(BitConverter.DoubleToInt64Bits), actual[output].ToArray().Select(BitConverter.DoubleToInt64Bits));
                    Assert.Equal(expected[output].ToArray().Select(BitConverter.DoubleToInt64Bits), mixed[output].ToArray().Select(BitConverter.DoubleToInt64Bits));
                }
                if (history == IndicatorHistoryMode.Full)
                {
                    var saved = bars.ToArray(); Array.Clear(bars);
                    int i = 0; await foreach (var snapshot in actual) Assert.Equal(saved[i++], snapshot.Bar);
                    Assert.Equal(count, i); saved.CopyTo(bars, 0);
                }
            }
        }
        finally { AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Theory]
    [InlineData(0, 2)] [InlineData(3, .1)] [InlineData(double.MaxValue, double.MaxValue)]
    public void SharedMomentsAndExtendedCancellationMatchIndependentRationals(double up, double down)
    {
        double[] prices = [double.MaxValue / 2, double.MaxValue, -double.MaxValue, 1, -1,
            double.Epsilon, -double.Epsilon, -0d, 0, 1, Math.BitIncrement(1), 2, 1];
        var bars = prices.Select(v => new Bar(default, 0, 1, -1, v, 1)).ToArray();
        foreach (int period in new[] { 1, 2, 3 })
        {
            var indicator = new ClassicDeviationBands(period, up, down);
            var expected = ClassicDeviationBandsReference.Values(bars, indicator);
            var state = new ClassicDeviationBands.SmaState(indicator);
            var actual = new double[6];
            for (int pass = 0; pass < 2; pass++)
            {
                state.Reset();
                for (int i = 0; i < bars.Length; i++)
                {
                    state.Update(in bars[i], actual);
                    for (int slot = 0; slot < 3; slot++)
                    {
                        Assert.Equal(BitConverter.DoubleToInt64Bits(expected[slot][i] ?? 0), BitConverter.DoubleToInt64Bits(actual[slot]));
                        Assert.Equal(expected[slot][i].HasValue ? 1d : 0, actual[slot + 3]);
                    }
                }
            }
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task LowerOverflowAndLaterInputErrorRetainFailureOrder(bool invalidInput)
    {
        var indicator = new ClassicDeviationBands(2, 0, double.MaxValue);
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
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancelled.Token));
        }
        Array.Fill(bars, new Bar(default, 0, 1, -1, 0, 1));
        using var recovered = await Builder(bars, indicator).BuildAsync();
        Assert.All(recovered[indicator.Lower].ToArray(), value => Assert.Equal(0, value));
        await Assert.ThrowsAsync<NotSupportedException>(() => Builder(bars, indicator).ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
    }
    private static StockIndicatorBuilder Builder(Bar[] bars, IIndicator indicator) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator);
}

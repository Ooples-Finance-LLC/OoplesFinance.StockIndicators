using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class SharedPriorChannelTests
{
    [Theory]
    [InlineData(1, 511)] [InlineData(1, 513)] [InlineData(20, 1025)]
    [InlineData(4095, 8201)] [InlineData(4096, 8201)] [InlineData(int.MaxValue, 1025)]
    public async Task PriorWindowReplayPreservesEverySlotAndSnapshot(int period, int count)
    {
        int prior = AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism = 8;
            var bars = Enumerable.Range(0, count).Select(i => new Bar(default, 0, i % 31 - 15, i % 31 - 17, 0, 1)).ToArray();
            var indicator = new PriorPriceChannel(period);
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
                Assert.All(actual[indicator.UpperIsDefined].ToArray().Take(Math.Min(count, period)), value => Assert.Equal(0, value));
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
    [InlineData(1)] [InlineData(2)] [InlineData(5)]
    public async Task ExtremePricesAndZeroMidpointMatchIndependentReference(int period)
    {
        double[] values = [double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon, 1, -1, -0d, 0, 1, Math.BitIncrement(1)];
        var bars = Enumerable.Range(0, 513).Select(i => new Bar(default, 0, values[i % values.Length], -values[(i + 1) % values.Length], 0, 1)).ToArray();
        var indicator = new PriorPriceChannel(period);
        var expected = PriceWindowChannelReference.Prior(bars, period);
        // The deque keeps the newest equal extremum. LINQ's scan oracle keeps
        // the first, which differs only for the sign of tied zeros here.
        for (int i = period; i < bars.Length; i++)
        {
            var window = bars.Skip(i - period).Take(period);
            expected[0][i] = window.Last(b => b.High == expected[0][i]).High;
            expected[1][i] = window.Last(b => b.Low == expected[1][i]).Low;
        }
        using var ordinary = await Builder(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync();
        using var actual = await Builder(bars, indicator).BuildAsync();
        for (int slot = 0; slot < 8; slot++)
        {
            var bits = actual[indicator.Outputs[slot]].ToArray().Select(BitConverter.DoubleToInt64Bits);
            Assert.Equal(expected[slot].Select(BitConverter.DoubleToInt64Bits), bits);
            Assert.Equal(ordinary[indicator.Outputs[slot]].ToArray().Select(BitConverter.DoubleToInt64Bits), bits);
        }
    }
    [Fact]
    public async Task FailureCancellationAndRequiredGpuRetainTheirContracts()
    {
        var bars = Enumerable.Range(0, 1025).Select(i => new Bar(default, 0, 2, 1, 0, 1)).ToArray();
        var indicator = new PriorPriceChannel(20);
        var builder = Builder(bars, indicator);
        using var saved = await builder.BuildAsync();
        bars[^1] = new Bar(default, 0, 2, 1, 0, double.NaN);
        var expected = await Record.ExceptionAsync(() => Builder(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync());
        var actual = await Record.ExceptionAsync(() => builder.BuildAsync());
        Assert.NotNull(expected); Assert.NotNull(actual);
        Assert.Equal(expected.GetType(), actual.GetType()); Assert.Equal(expected.Message, actual.Message); Assert.Null(builder.LastExecution);
        bars[^1] = bars[0]; using var recovered = await builder.BuildAsync();
        foreach (var output in indicator.Outputs) Assert.Equal(saved[output].ToArray(), recovered[output].ToArray());
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancelled.Token));
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
    }
    private static StockIndicatorBuilder Builder(Bar[] bars, IIndicator indicator) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator);
}

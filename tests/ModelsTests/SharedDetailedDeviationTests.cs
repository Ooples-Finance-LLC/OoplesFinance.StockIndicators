using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class SharedDetailedDeviationTests
{
    [Theory]
    [InlineData(2, null)] [InlineData(2, 1)] [InlineData(20, 20)]
    [InlineData(4095, 2)] [InlineData(4096, 2)] [InlineData(20, int.MaxValue)] [InlineData(int.MaxValue, 20)]
    public async Task TwoWindowReplayPreservesEveryOutputAndPresence(int period, int? smooth)
    {
        int previous = AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism = 8;
            foreach (int count in new[] { 0, 1, 511, 512, 513, period is 4095 or 4096 ? 8193 : 1025 })
            {
                var bars = Enumerable.Range(0, count).Select(i => new Bar(default, 0, 1, -1,
                    i % 61 < 25 ? 1 : (i % 31 - 15) / 8d, 1)).ToArray();
                if (count > 0 && period < 100) bars[0] = new Bar(default, 0, 1, -1, double.Epsilon, 1);
                var indicator = new StandardDeviationWithDetails(period, smooth);
                using var expected = await Builder(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync();
                foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
                {
                    var builder = Builder(bars, indicator).ConfigureHistory(history);
                    using var actual = await builder.BuildAsync();
                    using var mixed = await builder.ConfigureIndicators(indicator, new LowestLow(7)).BuildAsync();
                    foreach (var output in indicator.Outputs)
                    {
                        Assert.Equal(expected[output].ToArray().Select(BitConverter.DoubleToInt64Bits), actual[output].ToArray().Select(BitConverter.DoubleToInt64Bits));
                        Assert.Equal(expected[output].ToArray().Select(BitConverter.DoubleToInt64Bits), mixed[output].ToArray().Select(BitConverter.DoubleToInt64Bits));
                    }
                    Assert.Equal(actual[indicator.DeviationIsDefined].ToArray(), actual[indicator.MeanIsDefined].ToArray());
                    if (history == IndicatorHistoryMode.Full)
                    {
                        int i = 0; await foreach (var snapshot in actual) Assert.Equal(bars[i++], snapshot.Bar);
                        Assert.Equal(count, i);
                    }
                }
            }
        }
        finally { AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism = previous; }
    }

    [Fact]
    public async Task FiniteExtremeOutputsFailureRecoveryAndOwnershipStayIntact()
    {
        var indicator = new StandardDeviationWithDetails(2, 2);
        double[] prices = [double.MaxValue, -double.MaxValue, 0, -0d, double.Epsilon, -double.Epsilon, 1, -1];
        var bars = Enumerable.Range(0, 1025).Select(i => new Bar(default, 0, 1, -1, prices[i % prices.Length], 1)).ToArray();
        var builder = Builder(bars, indicator); using var saved = await builder.BuildAsync();
        foreach (var output in indicator.Outputs) Assert.All(saved[output].ToArray(), v => Assert.True(double.IsFinite(v)));
        var original = bars.ToArray(); bars[600] = new Bar(default, 0, 1, -1, 1, double.NaN);
        var expected = await Record.ExceptionAsync(() => Builder(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync());
        var actual = await Record.ExceptionAsync(() => builder.BuildAsync());
        Assert.NotNull(expected); Assert.NotNull(actual);
        Assert.Equal(expected.GetType(), actual.GetType()); Assert.Equal(expected.Message, actual.Message); Assert.Null(builder.LastExecution);
        original.CopyTo(bars, 0);
        using var recovered = await builder.BuildAsync();
        foreach (var output in indicator.Outputs) Assert.Equal(saved[output].ToArray(), recovered[output].ToArray());
        Array.Clear(bars); int i = 0; await foreach (var snapshot in saved) Assert.Equal(original[i++], snapshot.Bar);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancel.Token));
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
    }
    private static StockIndicatorBuilder Builder(Bar[] bars, IIndicator indicator) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator);
}

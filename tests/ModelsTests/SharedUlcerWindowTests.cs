using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class SharedUlcerWindowTests
{
    [Theory]
    [InlineData(1, 8193)]
    [InlineData(14, 8193)]
    [InlineData(4096, 8193)]
    [InlineData(4097, 8193)]
    [InlineData(int.MaxValue, 65)]
    [InlineData(14, 0)]
    [InlineData(14, 1)]
    public async Task WorkerOverlapMatchesEvaluatorAndPreservesOwnedOutputs(int period, int count)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            double[] prices = [10, 9, 8, 0, -1, 12, 11];
            var bars = Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i),
                10, 12, 0, period >= 4096 ? 10 : prices[i % prices.Length], 1)).ToArray();
            var indicator = new WindowUlcerIndex(period);
            using var expected = await Build(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync();
            foreach (int workers in new[] { 1, 8 })
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                CpuParallelSettings.MaxDegreeOfParallelism = workers;
                var builder = Build(bars, indicator).ConfigureHistory(history);
                using var actual = await builder.BuildAsync();
                actual.Dispose();
                foreach (var output in indicator.Outputs)
                    Assert.Equal(expected[output].ToArray().Select(BitConverter.DoubleToInt64Bits),
                        actual[output].ToArray().Select(BitConverter.DoubleToInt64Bits));
                if (count > 0)
                {
                    Assert.Equal(bars[^1], actual.Latest.Bar);
                    Assert.Equal(expected.Latest.IsWarmedUp, actual.Latest.IsWarmedUp);
                }
                if (history == IndicatorHistoryMode.Full && count > 0)
                {
                    Assert.Contains("Fused CPU values", builder.LastExecution!.Reason);
                    int index = 0;
                    await foreach (var snapshot in actual) Assert.Equal(bars[index++], snapshot.Bar);
                    Assert.Equal(count, index);
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Theory]
    [InlineData(1023)]
    [InlineData(8192)]
    public async Task InvalidInputPrecedesOverflowAndFailuresDoNotCorruptLaterRuns(int invalidIndex)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        try
        {
            var bars = Enumerable.Range(0, 8193).Select(i => new Bar(default, 0, 1, 0, 100 + i % 7, 1)).ToArray();
            var original = bars.ToArray();
            var indicator = new WindowUlcerIndex(2);
            var builder = Build(bars, indicator);
            using var saved = await builder.BuildAsync();
            var savedValues = saved[indicator.Value].ToArray();
            bars[0] = new Bar(default, 0, 1, 0, double.Epsilon, 1);
            bars[1] = new Bar(default, 0, 1, 0, -double.MaxValue, 1);
            await SameError(bars, indicator);
            bars[invalidIndex] = new Bar(default, 0, 1, 0, 100, double.NaN);
            await SameError(bars, indicator);
            Array.Copy(original, bars, bars.Length);
            using var recovered = await builder.BuildAsync();
            Assert.Equal(savedValues, recovered[indicator.Value].ToArray());
            Array.Clear(bars);
            Assert.Equal(savedValues, saved[indicator.Value].ToArray());
            int index = 0;
            await foreach (var snapshot in saved) Assert.Equal(original[index++], snapshot.Bar);
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancel.Token));
            Assert.Null(builder.LastExecution);
            await Assert.ThrowsAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    private static async Task SameError(Bar[] bars, WindowUlcerIndex indicator)
    {
        var expected = await Record.ExceptionAsync(() => Build(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync());
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var actual = await Record.ExceptionAsync(() => Build(bars, indicator).ConfigureHistory(history).BuildAsync());
            Assert.NotNull(expected); Assert.NotNull(actual);
            Assert.Equal(expected.GetType(), actual.GetType());
            Assert.Equal(expected.Message, actual.Message);
        }
    }
    private static StockIndicatorBuilder Build(Bar[] bars, IIndicator indicator) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).ConfigureExecution(IndicatorExecutionBackend.Cpu);
}

using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class SharedRelativeVolatilityWindowTests
{
    [Theory]
    [InlineData(2, 8193, false)]
    [InlineData(14, 8193, false)]
    [InlineData(2, 8193, true)]
    [InlineData(14, 8193, true)]
    [InlineData(2048, 8193, false)]
    [InlineData(2049, 8193, true)]
    [InlineData(int.MaxValue, 65, true)]
    [InlineData(1073741823, 65, false)]
    [InlineData(1073741824, 65, true)]
    [InlineData(14, 0, true)]
    [InlineData(14, 1, false)]
    public async Task WorkerOverlapMatchesEvaluatorAndPreservesOwnedOutputs(int period, int count, bool mixed)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            double[] prices = [10, 9, 8, 0, -1, 12, 11];
            var bars = Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i),
                10, 12, 0, prices[i % prices.Length], 1)).ToArray();
            var indicator = new RelativeVolatilityStrength(period);
            // Place extended-range transitions at and just before worker seams.
            if (period == 14 && !mixed && count > 2048)
                foreach (int index in new[] { 1022, 1023, 1024, 2047, 2048 })
                    bars[index] = new Bar(default, 0, 1, 0, index % 2 == 0 ? double.MaxValue : -double.MaxValue, 1);
            IIndicator[] roots = mixed ? [indicator, new Wma(7)] : [indicator];
            using var expected = await Build(bars, roots).ConfigureBehavior(_ => { }).BuildAsync();
            foreach (int workers in new[] { 1, 8 })
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                CpuParallelSettings.MaxDegreeOfParallelism = workers;
                var builder = Build(bars, roots).ConfigureHistory(history);
                using var actual = await builder.BuildAsync();
                actual.Dispose();
                foreach (var output in roots.SelectMany(root => root.Outputs))
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
    public async Task InvalidCapturedInputAndCancellationDoNotCorruptLaterRuns(int invalidIndex)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        try
        {
            var bars = Enumerable.Range(0, 8193).Select(i => new Bar(default, 0, 1, 0, 100 + i % 7, 1)).ToArray();
            var original = bars.ToArray();
            var indicator = new RelativeVolatilityStrength(2);
            var builder = Build(bars, indicator);
            using var saved = await builder.BuildAsync();
            var savedValues = saved[indicator.Value].ToArray();
            bars[0] = new Bar(default, 0, 1, 0, double.Epsilon, 1);
            bars[1] = new Bar(default, 0, 1, 0, -double.MaxValue, 1);
            using (var extended = await builder.BuildAsync())
                Assert.All(extended[indicator].ToArray(), value => Assert.True(double.IsFinite(value)));
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

    private static async Task SameError(Bar[] bars, RelativeVolatilityStrength indicator)
    {
        foreach (IIndicator[] roots in new IIndicator[][] { [indicator], [indicator, new Wma(7)] })
        {
            var expected = await Record.ExceptionAsync(() => Build(bars, roots).ConfigureBehavior(_ => { }).BuildAsync());
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                var actual = await Record.ExceptionAsync(() => Build(bars, roots).ConfigureHistory(history).BuildAsync());
                Assert.NotNull(expected); Assert.NotNull(actual);
                Assert.Equal(expected.GetType(), actual.GetType());
                Assert.Equal(expected.Message, actual.Message);
            }
        }
    }
    private static StockIndicatorBuilder Build(Bar[] bars, params IIndicator[] indicators) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicators).ConfigureExecution(IndicatorExecutionBackend.Cpu);
}

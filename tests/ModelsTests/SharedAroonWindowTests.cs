using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class SharedAroonWindowTests
{
    public static IEnumerable<object[]> Configurations => from period in new[] { 1, 3, 20, 4096, 4097, int.MaxValue }
        from extra in new[] { false, true } from oldest in new[] { false, true } select new object[] { period, extra, oldest };

    [Theory, MemberData(nameof(Configurations))]
    public async Task MultiOutputWorkerSeedsMatchEvaluatorAcrossWindowAndTieConventions(int period, bool extra, bool oldest)
    {
        int previous = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            int count = period == int.MaxValue ? 65 : 8193;
            double[] values = [-0d, 0d, -1, 2, 2, double.MaxValue, double.Epsilon, -double.MaxValue, -double.Epsilon];
            var bars = Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0,
                values[i % values.Length], values[(i + 4) % values.Length], 0, 1)).ToArray();
            var indicator = new WindowAroon(period, extra, oldest);
            foreach (int workers in new[] { 1, 8 })
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                CpuParallelSettings.MaxDegreeOfParallelism = workers;
                using var expected = await Build(bars, history, indicator).ConfigureBehavior(_ => { }).BuildAsync();
                using var actual = await Build(bars, history, indicator).BuildAsync();
                Equal(expected, actual, indicator);
                Assert.Equal(expected.Latest.IsWarmedUp, actual.Latest.IsWarmedUp);
                if (history == IndicatorHistoryMode.Full)
                {
                    int i = 0;
                    await foreach (var snapshot in actual) Assert.Equal(bars[i++], snapshot.Bar);
                    Assert.Equal(count, i);
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = previous; }
    }

    [Fact]
    public async Task InvalidWorkerInputsCancellationAndMixedRootsRetainOwnedResults()
    {
        int previous = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            var bars = Enumerable.Range(0, 8193).Select(i => new Bar(default, 0, i % 17, -(i % 13), i % 7, 1)).ToArray();
            var indicator = new WindowAroon(20);
            var mixed = new WindowExtremeIndex(7);
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                using var saved = await Build(bars, history, indicator).BuildAsync();
                for (int slot = 0; slot < 6; slot++) Assert.Equal(bars.Length, saved[indicator.Outputs[slot]].Length);
                using var expected = await Build(bars, history, indicator, mixed).ConfigureBehavior(_ => { }).BuildAsync();
                using var actual = await Build(bars, history, indicator, mixed).BuildAsync();
                Equal(expected, actual, indicator);
                Assert.Equal(expected[mixed].ToArray(), actual[mixed].ToArray());
                foreach (int at in new[] { 0, 1004, 1023, 1024, 4095, 8192 })
                {
                    var original = bars[at];
                    bars[at] = new Bar(default, 0, 1, -1, 0, double.NaN);
                    var evaluator = await Record.ExceptionAsync(() => Build(bars, history, indicator).ConfigureBehavior(_ => { }).BuildAsync());
                    var builder = Build(bars, history, indicator);
                    var failure = await Record.ExceptionAsync(() => builder.BuildAsync());
                    Assert.NotNull(evaluator); Assert.NotNull(failure);
                    Assert.Equal(evaluator.GetType(), failure.GetType()); Assert.Equal(evaluator.Message, failure.Message);
                    Assert.Null(builder.LastExecution);
                    bars[at] = original;
                }
                using var recovered = await Build(bars, history, indicator).BuildAsync();
                Equal(saved, recovered, indicator);
                using var cancel = new CancellationTokenSource(); cancel.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Build(bars, history, indicator).BuildAsync(cancel.Token));
                using var empty = await Build([], history, indicator).BuildAsync();
                Assert.Equal(0, empty.BarCount);
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = previous; }
    }

    [Fact]
    public async Task EarlyWorkerThresholdKeepsEveryOutputAndInputFailure()
    {
        int previous = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            foreach (int count in new[] { 511, 512, 513 })
            foreach (bool extra in new[] { false, true })
            foreach (bool oldest in new[] { false, true })
            {
                var bars = Enumerable.Range(0, count).Select(i => new Bar(default, 0, i % 7, -(i % 5), 0, 1)).ToArray();
                var indicator = new WindowAroon(20, extra, oldest);
                foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
                {
                    using var expected = await Build(bars, history, indicator).ConfigureBehavior(_ => { }).BuildAsync();
                    using var actual = await Build(bars, history, indicator).BuildAsync();
                    Equal(expected, actual, indicator);
                }
                bars[63] = new Bar(default, double.NaN, 1, -1, 0, 1);
                var evaluator = await Record.ExceptionAsync(() => Build(bars, IndicatorHistoryMode.Full, indicator).ConfigureBehavior(_ => { }).BuildAsync());
                var failure = await Record.ExceptionAsync(() => Build(bars, IndicatorHistoryMode.Full, indicator).BuildAsync());
                Assert.NotNull(evaluator); Assert.NotNull(failure);
                Assert.Equal(evaluator.GetType(), failure.GetType()); Assert.Equal(evaluator.Message, failure.Message);
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = previous; }
    }

    private static StockIndicatorBuilder Build(Bar[] bars, IndicatorHistoryMode history, params IIndicator[] indicators) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicators).ConfigureHistory(history);
    private static void Equal(IIndicatorRun expected, IIndicatorRun actual, WindowAroon indicator)
    {
        for (int slot = 0; slot < 6; slot++) Assert.Equal(expected[indicator.Outputs[slot]].ToArray().Select(BitConverter.DoubleToInt64Bits),
            actual[indicator.Outputs[slot]].ToArray().Select(BitConverter.DoubleToInt64Bits));
    }
}

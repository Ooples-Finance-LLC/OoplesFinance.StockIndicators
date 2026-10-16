using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class SharedDispersionWindowTests
{
    [Theory]
    [InlineData(WindowDispersionOutput.Variance)]
    [InlineData(WindowDispersionOutput.StandardDeviation)]
    [InlineData(WindowDispersionOutput.ZScore)]
    public async Task WorkerReplayPreservesExactBitsAcrossGridHistoryAndAllConfigurations(WindowDispersionOutput output)
    {
        int prior = AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism = 8;
            foreach (int period in new[] { 1, 2, 20, 4096, 4097, int.MaxValue })
            foreach (bool sample in new[] { false, true })
            foreach (double multiplier in new[] { 0d, 1d, -2d })
            {
                int count = period == 4096 || period == 4097 ? 8193 : 513;
                var bars = Enumerable.Range(0, count).Select(i => new Bar(default, 0, 1, -1, (i % 31 - 15) / 8d, 1)).ToArray();
                // Serial state retains this very fine grid after expiry. Later
                // workers must still publish identical bits using their coarser grid.
                if (count == 513) bars[0] = new Bar(default, 0, 1, -1, double.Epsilon, 1);
                var indicator = new WindowDispersion(period, output, sample, multiplier);
                using var ordinary = await Builder(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync();
                foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
                {
                    var builder = Builder(bars, indicator).ConfigureHistory(history);
                    using var shared = await builder.BuildAsync();
                    Assert.Equal(ordinary[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits), shared[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits));
                    using var mixed = await builder.ConfigureIndicators(indicator, new LowestLow(7)).BuildAsync();
                    Assert.Equal(ordinary[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits), mixed[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits));
                    if (history == IndicatorHistoryMode.Full)
                    {
                        int i = 0; await foreach (var snapshot in shared) Assert.Equal(bars[i++], snapshot.Bar);
                        Assert.Equal(count, i);
                    }
                }
            }
        }
        finally { AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Theory]
    [InlineData(511)] [InlineData(512)] [InlineData(513)]
    public async Task FailureOrderingRecoveryAndCancellationSurviveSchedulingBoundary(int count)
    {
        var indicator = new WindowDispersion(2, WindowDispersionOutput.Variance);
        var bars = Enumerable.Repeat(new Bar(default, 0, 1, -1, 1, 1), count).ToArray();
        var builder = Builder(bars, indicator); using var saved = await builder.BuildAsync();
        foreach (bool invalid in new[] { false, true })
        {
            bars[1] = new Bar(default, 0, 1, -1, double.MaxValue, 1);
            if (invalid) bars[^1] = new Bar(default, 0, 1, -1, 1, double.NaN);
            var expected = await Record.ExceptionAsync(() => Builder(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync());
            var actual = await Record.ExceptionAsync(() => builder.BuildAsync());
            Assert.NotNull(expected); Assert.NotNull(actual);
            Assert.Equal(expected.GetType(), actual.GetType()); Assert.Equal(expected.Message, actual.Message); Assert.Null(builder.LastExecution);
        }
        Array.Fill(bars, new Bar(default, 0, 1, -1, 1, 1));
        using var recovered = await builder.BuildAsync(); Assert.Equal(saved[indicator].ToArray(), recovered[indicator].ToArray());
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancel.Token));
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
    }
    private static StockIndicatorBuilder Builder(Bar[] bars, IIndicator indicator) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator);
}

using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class AsinSharedExecutionTests
{
    [Theory]
    [InlineData(0, 1)] [InlineData(0, 8)] [InlineData(1, 1)] [InlineData(1, 8)]
    [InlineData(17, 1)] [InlineData(17, 8)] [InlineData(511, 1)] [InlineData(511, 8)]
    [InlineData(512, 1)] [InlineData(512, 8)] [InlineData(513, 1)] [InlineData(513, 8)]
    [InlineData(1001, 1)] [InlineData(1001, 8)] [InlineData(8191, 1)] [InlineData(8191, 8)]
    [InlineData(8192, 1)] [InlineData(8192, 8)] [InlineData(8193, 1)] [InlineData(8193, 8)]
    public async Task BothOwnedColumnsRetainDomainEdgesAndSignedZero(int count, int workers)
    {
        int previous = AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism = workers;
            await CheckOwnedColumns(count);
        }
        finally { AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism = previous; }
    }
    private static async Task CheckOwnedColumns(int count)
    {
        double[] edges = [-0d, 0d, -1, 1, double.Epsilon, -double.Epsilon, Math.BitDecrement(1), Math.BitIncrement(-1), -2, 2, double.MaxValue];
        var bars = Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), i, i + 2, i - 1, edges[i % edges.Length], i + 1)).ToArray();
        var indicator = new PriceCircularTransform(PriceCircularOperation.ArcSine);
        Assert.True(ValuesBarExecution.SupportsOwned([indicator]));
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var input = bars.ToArray();
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input))
                .ConfigureIndicators(indicator).ConfigureHistory(mode).BuildAsync();
            Array.Clear(input); run.Dispose();
            Assert.Equal(bars.Select(b => BitConverter.DoubleToInt64Bits(b.Close is >= -1 and <= 1 ? Math.Asin(b.Close) : 0)),
                run[indicator.Value].ToArray().Select(BitConverter.DoubleToInt64Bits));
            Assert.Equal(bars.Select(b => b.Close is >= -1 and <= 1 ? 1d : 0), run[indicator.IsDefined].ToArray());
            if (count > 0) Assert.Equal(bars[^1], run.Latest.Bar);
            if (mode == IndicatorHistoryMode.Full)
            {
                int row = 0; await foreach (var snapshot in run) Assert.Equal(bars[row++], snapshot.Bar);
                Assert.Equal(count, row);
            }
        }
    }
    [Theory]
    [InlineData(513)] [InlineData(8193)]
    public async Task ParallelBoundaryValidationAndCancellationRecover(int count)
    {
        int previous = AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism = 8;
            await CheckRecovery(count);
        }
        finally { AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism = previous; }
    }
    private static async Task CheckRecovery(int count)
    {
        var bars = Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 1, 2, 0, .5, 1)).ToArray();
        var indicator = new PriceCircularTransform(PriceCircularOperation.ArcSine);
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).ConfigureHistory(mode);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
            var valid = bars[count / 2]; bars[count / 2] = new(valid.Time, 1, 2, 0, .5, double.NaN);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
            bars[count / 2] = valid;
            using var run = await builder.ConfigureExecution(IndicatorExecutionBackend.Auto).BuildAsync();
            Assert.Equal(IndicatorExecutionBackend.Cpu, builder.LastExecution!.Backend);
            Assert.All(run[indicator.Value].ToArray(), v => Assert.Equal(Math.Asin(.5), v));
        }
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var gpu = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(indicator).ConfigureHistory(mode).ConfigureExecution(IndicatorExecutionBackend.Gpu);
            if (!TensorsGpuExecution.TryGet(out _, out _))
                await Assert.ThrowsAsync<NotSupportedException>(() => gpu.BuildAsync());
            else
            {
                using var run = await gpu.BuildAsync();
                Assert.Equal(IndicatorExecutionBackend.Gpu, gpu.LastExecution!.Backend);
                Assert.All(run[indicator.Value].ToArray(), v => Assert.InRange(Math.Abs(v - Math.Asin(.5)), 0, 2e-15));
                Assert.All(run[indicator.IsDefined].ToArray(), v => Assert.Equal(1d, v));
            }
        }
    }
}

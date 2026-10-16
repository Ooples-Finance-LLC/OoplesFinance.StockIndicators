using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class FibonacciWeightedAverageTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(20)]
    [InlineData(1500)]
    public void RecurrenceAndResetMatchCompleteWindow(int period)
    {
        double[] values = [-0d, 0d, double.Epsilon, -double.Epsilon, double.MaxValue,
            -double.MaxValue, 1, -.125, 1e-200];
        var bars = Enumerable.Range(0, period == 1500 ? 1503 : 91)
            .Select(i => new Bar(default, 0, 1, 0, values[i % values.Length], 1)).ToArray();
        var expected = FibonacciWeightedSnapshot.Calculate(bars, period);
        var state = (IIndicatorState)new FibonacciWeightedAverage(period).CreateState();
        for (int pass = 0; pass < 2; pass++)
        {
            state.Reset();
            for (int i = 0; i < bars.Length; i++)
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i]), BitConverter.DoubleToInt64Bits(state.Update(bars[i])));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(511)]
    [InlineData(512)]
    [InlineData(513)]
    [InlineData(8193)]
    public async Task BuilderPreservesValuesAcrossWorkerBoundaries(int count)
    {
        var prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            var bars = Enumerable.Range(0, count).Select(i => new Bar(default, 0, 1, 0, (i * 17 % 97 - 48) / 8d, 1)).ToArray();
            var expected = FibonacciWeightedSnapshot.Calculate(bars, 20);
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
            {
                var indicator = new FibonacciWeightedAverage(20);
                using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                    .ConfigureIndicators(indicator).ConfigureHistory(mode).BuildAsync();
                Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), run[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits));
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Fact]
    public async Task ValidationCancellationAndGpuRejectionRemainExplicit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FibonacciWeightedAverage(0));
        var bars = new[] { new Bar(default, 0, 1, 0, 1, double.NaN) };
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(new FibonacciWeightedAverage());
        await Assert.ThrowsAnyAsync<ArgumentException>(() => builder.BuildAsync());
        bars[0] = new Bar(default, 0, 1, 0, 1, 1);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancellation.Token));
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
    }
}

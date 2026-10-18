using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class SharedValidReturnTests
{
    private static double Expected(double current, double previous) => previous == 0 ? 0
        : ((ReferenceFraction.FromDouble(current) - ReferenceFraction.FromDouble(previous))
            / ReferenceFraction.FromDouble(previous) * new ReferenceFraction(100)).ToDouble();

    [Fact]
    public void ReusedArithmeticPreservesExactBitsAndReset()
    {
        double[] edges = [-0d, 0d, double.Epsilon, -double.Epsilon, double.MaxValue, -double.MaxValue, 1, -1, Math.BitIncrement(1), .1];
        var state = new RateOfChangeWithValidity.State(1);
        var output = new double[2];
        foreach (double previous in edges)
        foreach (double current in edges)
        {
            state.Reset();
            var bar = new Bar(default, 0, 1, -1, previous, 1); state.Update(in bar, output);
            Assert.Equal(new double[2], output);
            bar = new Bar(default, 0, 1, -1, current, 1); state.Update(in bar, output);
            Assert.Equal(BitConverter.DoubleToInt64Bits(Expected(current, previous)), BitConverter.DoubleToInt64Bits(output[0]));
            Assert.Equal(previous == 0 ? 0d : 1d, output[1]);
        }
    }

    [Theory]
    [InlineData(1)] [InlineData(20)] [InlineData(4095)] [InlineData(4096)] [InlineData(int.MaxValue)]
    public async Task WorkerAndFallbackPathsPreserveBothOutputsAndSnapshots(int period)
    {
        int previous = AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism = 8;
            double[] prices = [-0d, 0d, -1, 1, Math.BitIncrement(1), .1, -7, 3, 3];
            foreach (int count in new[] { 0, 1, 511, 512, 513, 8193 })
            {
                var bars = Enumerable.Range(0, count).Select(i => new Bar(default, 0, 1, -1, prices[(i + i / 37) % prices.Length], 1)).ToArray();
                var values = new double[count]; var defined = new double[count];
                for (int i = period; i < count; i++)
                {
                    values[i] = Expected(bars[i].Close, bars[i - period].Close);
                    defined[i] = bars[i - period].Close == 0 ? 0 : 1;
                }
                var indicator = new RateOfChangeWithValidity(period);
                foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
                {
                    var builder = Builder(bars, indicator, history);
                    using var run = await builder.BuildAsync();
                    Assert.Equal(values.Select(BitConverter.DoubleToInt64Bits), run[indicator.Value].ToArray().Select(BitConverter.DoubleToInt64Bits));
                    Assert.Equal(defined, run[indicator.IsDefined].ToArray());
                    using var mixed = await builder.ConfigureIndicators(indicator, new LowestLow(7)).BuildAsync();
                    Assert.Equal(run[indicator.Value].ToArray(), mixed[indicator.Value].ToArray());
                    Assert.Equal(defined, mixed[indicator.IsDefined].ToArray());
                    if (history == IndicatorHistoryMode.Full)
                    {
                        int i = 0;
                        await foreach (var snapshot in run) Assert.Equal(bars[i++], snapshot.Bar);
                        Assert.Equal(count, i);
                    }
                }
            }
        }
        finally { AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism = previous; }
    }

    [Fact]
    public async Task OrderedFailuresCancellationAndOwnedHistoryRemainIntact()
    {
        var bars = Enumerable.Range(0, 8193).Select(i => new Bar(default, 0, 1, -1, 1 + i % 13, 1)).ToArray();
        var indicator = new RateOfChangeWithValidity(1);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var builder = Builder(bars, indicator, history);
            using var run = await builder.BuildAsync();
            var initial = bars.ToArray();
            bars[0] = new Bar(default, 0, 1, -1, double.Epsilon, 1);
            bars[1] = new Bar(default, 0, 1, -1, double.MaxValue, 1);
            await Assert.ThrowsAsync<IndicatorOutputException>(() => builder.BuildAsync());
            Assert.Null(builder.LastExecution);
            bars[^1] = new Bar(default, 0, 1, -1, 1, double.NaN);
            var ordinary = await Record.ExceptionAsync(() => Builder(bars, indicator, history).ConfigureBehavior(_ => { }).BuildAsync());
            var shared = await Record.ExceptionAsync(() => builder.BuildAsync());
            Assert.NotNull(ordinary); Assert.NotNull(shared);
            Assert.Equal(ordinary.GetType(), shared.GetType()); Assert.Equal(ordinary.Message, shared.Message);
            initial.CopyTo(bars, 0);
            using var recovered = await builder.BuildAsync(); Assert.Equal(run[indicator.Value].ToArray(), recovered[indicator.Value].ToArray());
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancel.Token));
            Array.Clear(bars);
            if (history == IndicatorHistoryMode.Full)
            {
                int i = 0; await foreach (var snapshot in run) Assert.Equal(initial[i++], snapshot.Bar);
            }
            initial.CopyTo(bars, 0);
        }
        await Assert.ThrowsAsync<NotSupportedException>(() => Builder(bars, indicator, IndicatorHistoryMode.Full)
            .ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
    }

    private static StockIndicatorBuilder Builder(Bar[] bars, IIndicator indicator, IndicatorHistoryMode history) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).ConfigureHistory(history);
}

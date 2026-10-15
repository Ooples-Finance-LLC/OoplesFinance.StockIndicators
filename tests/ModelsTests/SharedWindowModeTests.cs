using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class SharedWindowModeTests
{
    [Fact]
    public void UnchangedWindowsRetainFutureEvictionAndReset()
    {
        double[] cycle = [-0d, 0d, double.MaxValue, -double.MaxValue, 1, 1, double.Epsilon, -double.Epsilon];
        var values = Enumerable.Range(0, 40).Select(i => cycle[i % cycle.Length])
            .Concat(Enumerable.Repeat(3d, 12)).Concat(cycle).ToArray();
        var state = new WindowMode.State(cycle.Length);
        foreach (var input in new[] { values, values.Reverse().ToArray() })
        {
            state.Reset();
            var expected = Oracle(input, cycle.Length).Select(BitConverter.DoubleToInt64Bits);
            var actual = input.Select(v =>
            {
                var bar = new Bar(default, 0, 1, -1, v, 1);
                return BitConverter.DoubleToInt64Bits(state.Update(in bar));
            });
            Assert.Equal(expected, actual);
        }
    }
    private static double[] Oracle(double[] values, int period) => Enumerable.Range(0, values.Length).Select(i =>
    {
        var window = values.Skip(Math.Max(0, i - period + 1)).Take(Math.Min(period, i + 1)).ToArray();
        if (window.Length == period)
        {
            var groups = window.GroupBy(v => v).ToArray();
            int maximum = groups.Max(g => g.Count());
            window = groups.Where(g => g.Count() == maximum).Select(g => g.Key).ToArray();
        }
        var sum = window.Aggregate(new ReferenceFraction(0), (a, v) => a + ReferenceFraction.FromDouble(v));
        return (sum / new ReferenceFraction(window.Length)).ToDouble();
    }).ToArray();

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(20)]
    [InlineData(4096)] [InlineData(int.MaxValue)]
    public async Task BoundedReplayAndResetPreserveExactMeans(int period)
    {
        int previous = AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism = 8;
            double[] edges = [-0d, 0d, double.Epsilon, -double.Epsilon, double.MaxValue, -double.MaxValue, 1, Math.BitIncrement(1), 1, 1, -1];
            foreach (int count in new[] { 511, 512, 513, 1025 })
            {
                var values = Enumerable.Range(0, count).Select(i => edges[(i + i / 37) % edges.Length]).ToArray();
                var bars = values.Select(v => new Bar(default, 0, 1, -1, v, 1)).ToArray();
                var expected = Oracle(values, period).Select(BitConverter.DoubleToInt64Bits).ToArray();
                var state = new WindowMode.State(period);
                for (int replay = 0; replay < 2; replay++)
                {
                    state.Reset();
                    Assert.Equal(expected, bars.Select(b => BitConverter.DoubleToInt64Bits(state.Update(in b))));
                }
                var indicator = new WindowMode(period);
                foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
                {
                    var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).ConfigureHistory(history);
                    using var run = await builder.BuildAsync();
                    Assert.Equal(expected, run[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits));
                    if (history == IndicatorHistoryMode.Full)
                    {
                        int i = 0;
                        await foreach (var snapshot in run) Assert.Equal(bars[i++], snapshot.Bar);
                        Assert.Equal(count, i);
                    }
                    using var mixed = await builder.ConfigureIndicators(indicator, new LowestLow(7)).BuildAsync();
                    Assert.Equal(expected, mixed[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits));
                }
            }
        }
        finally { AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism = previous; }
    }

    [Fact]
    public async Task InvalidInputCancellationAndReuseRemainSafe()
    {
        var indicator = new WindowMode(3);
        var bars = Enumerable.Range(0, 1025).Select(i => new Bar(default, 0, 1, -1, i % 7, 1)).ToArray();
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).ConfigureHistory(history);
            using var original = await builder.BuildAsync();
            var saved = bars[600];
            bars[600] = new Bar(default, 0, 1, -1, double.NaN, 1);
            Assert.NotNull(await Record.ExceptionAsync(() => builder.BuildAsync()));
            Assert.Null(builder.LastExecution);
            bars[600] = saved;
            using var recovered = await builder.BuildAsync();
            Assert.Equal(original[indicator].ToArray(), recovered[indicator].ToArray());
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancel.Token));
        }
        var state = new WindowMode.State(3);
        var valid = bars[0];
        for (int i = 0; i < 4; i++) state.Update(in valid);
        foreach (double value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var invalid = new Bar(default, 0, 1, -1, value, 1);
            Assert.Throws<ArithmeticException>(() => state.Update(in invalid));
        }
    }
}

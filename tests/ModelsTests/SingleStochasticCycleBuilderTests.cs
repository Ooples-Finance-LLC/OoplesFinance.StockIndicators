using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SingleStochasticCycleBuilderTests
{
    private static Bar[] Prices(int count) => Enumerable.Range(0, count).Select(i =>
    {
        double value = 100 + (i * 13 % 101) / 8d;
        return new Bar(DateTime.UnixEpoch.AddMinutes(i), value, value, value, value, 1);
    }).ToArray();

    [Theory]
    [InlineData(1, 1, 2)]
    [InlineData(3, 2, 5)]
    [InlineData(10, 23, 50)]
    [InlineData(int.MaxValue, 1, 2)]
    [InlineData(int.MaxValue, int.MaxValue - 1, int.MaxValue)]
    public async Task HistoryModesPreserveValuesPresenceAndOwnedBars(int cycle, int fast, int slow)
    {
        var bars = Prices(100); var original = bars.ToArray();
        var expected = SchaffTrendCycleSnapshot.Calculate(bars, cycle, fast, slow);
        var indicator = new SingleStochasticCycle(cycle, fast, slow);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).ConfigureHistory(history);
            using var run = await builder.BuildAsync(); run.Dispose();
            Assert.Equal(expected.Select(v => BitConverter.DoubleToInt64Bits(v ?? 0)), run[indicator.Value].ToArray().Select(BitConverter.DoubleToInt64Bits));
            Assert.Equal(expected.Select(v => v.HasValue ? 1d : 0), run[indicator.IsDefined].ToArray());
            Assert.Equal(original[^1], run.Latest.Bar);
            if (history == IndicatorHistoryMode.Full)
            {
                Assert.Contains("Fused CPU values", builder.LastExecution!.Reason);
                Array.Clear(bars); int index = 0;
                await foreach (var snapshot in run) Assert.Equal(original[index++], snapshot.Bar);
                Array.Copy(original, bars, bars.Length);
            }
        }
    }

    [Fact]
    public void ExtremaWrapGrowthTiesAndResetMatchBatchScanning()
    {
        foreach (int cycle in new[] { 1, 2, 3, 7, 31 })
        foreach (int pattern in new[] { 0, 1, 2, 3 })
        {
            var bars = Enumerable.Range(0, 160).Select(i =>
            {
                double price = pattern switch { 0 => 4d, 1 => i % 35, 2 => -(i % 35), _ => (i * 37 % 19) - 9d };
                return new Bar(default, price, price, price, price, 0);
            }).ToArray();
            var state = new SingleStochasticCycle.State(cycle, 1, 3); var output = new double[2];
            foreach (bool reverse in new[] { false, true })
            {
                if (reverse) Array.Reverse(bars);
                state.Reset(); var expected = SchaffTrendCycleSnapshot.Calculate(bars, cycle, 1, 3);
                for (int i = 0; i < bars.Length; i++)
                {
                    state.Update(bars[i], output);
                    Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i] ?? 0), BitConverter.DoubleToInt64Bits(output[0]));
                    Assert.Equal(expected[i].HasValue ? 1d : 0, output[1]);
                }
            }
        }
    }

    [Fact]
    public void OrdinaryReplayAfterResetAllocatesNothing()
    {
        var bars = Prices(1000); var state = new SingleStochasticCycle.State(10, 23, 50); var output = new double[2];
        void Replay() { foreach (var bar in bars) state.Update(bar, output); }
        for (int i = 0; i < 3; i++) { state.Reset(); Replay(); }
        double expected = output[0]; state.Reset();
        long before = GC.GetAllocatedBytesForCurrentThread(); Replay();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before); Assert.Equal(expected, output[0]);
    }

    [Theory]
    [InlineData(3, 2, 5)]
    [InlineData(int.MaxValue, 1, 2)]
    public async Task LifecycleMatchesIndependentReference(int cycle, int fast, int slow)
    {
        var report = await IndicatorValidation.ValidateAsync(new IndicatorValidationCase(typeof(SingleStochasticCycle),
            "single stochastic cycle", () => new SingleStochasticCycle(cycle, fast, slow)));
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public async Task InvalidInputCancellationAndRequiredGpuAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SingleStochasticCycle(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SingleStochasticCycle(fastPeriod: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SingleStochasticCycle(fastPeriod: 3, slowPeriod: 3));
        var bars = Prices(100); bars[^1] = new Bar(default, 1, 1, 1, 1, double.NaN);
        StockIndicatorBuilder Build() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(new SingleStochasticCycle());
        var expected = await Record.ExceptionAsync(() => Build().ConfigureBehavior(_ => { }).BuildAsync());
        var actual = await Record.ExceptionAsync(() => Build().BuildAsync());
        Assert.NotNull(expected); Assert.NotNull(actual); Assert.Equal(expected.GetType(), actual.GetType()); Assert.Equal(expected.Message, actual.Message);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Build().BuildAsync(cancellation.Token));
        await Assert.ThrowsAsync<NotSupportedException>(() => Build().ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
    }
}

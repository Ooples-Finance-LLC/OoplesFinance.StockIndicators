using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class MedianAdaptiveAverageBuilderTests
{
    private static Bar[] BarsFor(double[] prices) => prices.Select((p, i) =>
        new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(9)]
    [InlineData(int.MaxValue)]
    public async Task BuilderMatchesSnapshotAndRetainsOwnedHistory(int period)
    {
        var bars = BarsFor(Enumerable.Range(0, 80).Select(i => (i * 13 % 31) - 10d).ToArray());
        var savedBars = bars.ToArray();
        var indicator = new MedianAdaptiveAverage(period);
        var expected = MedianAdaptiveSnapshot.Calculate(bars, period);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(indicator).ConfigureHistory(history);
            using var run = await builder.BuildAsync();
            run.Dispose();
            Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), run[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits));
            Assert.Equal(history == IndicatorHistoryMode.Full
                ? "Fused CPU values execution with owned history."
                : "CPU values execution without retained bar history.", builder.LastExecution!.Reason);
            Assert.Equal(savedBars[^1], run.Latest.Bar);
            if (history == IndicatorHistoryMode.Full)
            {
                Array.Clear(bars);
                int index = 0;
                await foreach (var snapshot in run) Assert.Equal(savedBars[index++], snapshot.Bar);
                Array.Copy(savedBars, bars, bars.Length);
            }
        }
    }

    [Fact]
    public void DescendingMultisetsMatchSnapshotForDuplicatesSignsAndThresholdTies()
    {
        var random = new Random(971);
        foreach (int period in new[] { 1, 2, 3, 6, 9, 39 })
        foreach (double threshold in new[] { -double.MaxValue, -.2, 0, .002, Math.BitDecrement(.2), .2, Math.BitIncrement(.2), double.MaxValue })
        {
            var bars = BarsFor(Enumerable.Range(0, 100).Select(_ => (double)random.Next(-3, 4)).ToArray());
            var state = new MedianAdaptiveAverage.State(period, threshold);
            var expected = MedianAdaptiveSnapshot.Calculate(bars, period, threshold);
            foreach (bool reverse in new[] { false, true })
            {
                if (reverse) { Array.Reverse(bars); expected = MedianAdaptiveSnapshot.Calculate(bars, period, threshold); }
                state.Reset();
                for (int i = 0; i < bars.Length; i++)
                    Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i]), BitConverter.DoubleToInt64Bits(state.Update(bars[i])));
            }
        }
    }

    [Fact]
    public void WarmedOrdinaryUpdatesAndResetReuseAllBuffers()
    {
        var bars = BarsFor(Enumerable.Range(0, 1000).Select(i => 100 + (i * 13 % 101) / 8d).ToArray());
        var state = new MedianAdaptiveAverage.State(39, .002);
        double value = 0;
        void Replay() { foreach (var bar in bars) value = state.Update(bar); }
        for (int i = 0; i < 3; i++) { state.Reset(); Replay(); }
        double expected = value;
        state.Reset();
        long before = GC.GetAllocatedBytesForCurrentThread();
        Replay();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(int.MaxValue)]
    public async Task LifecycleMatchesIndependentBatchReference(int period)
    {
        var report = await IndicatorValidation.ValidateAsync(new IndicatorValidationCase(typeof(MedianAdaptiveAverage),
            "median adaptive", () => new MedianAdaptiveAverage(period)));
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public async Task InvalidInputCancellationAndConfigurationAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MedianAdaptiveAverage(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MedianAdaptiveAverage(threshold: double.NaN));
        var bars = BarsFor([1, 2, double.NaN]);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(new MedianAdaptiveAverage());
        var expected = await Record.ExceptionAsync(() => builder.ConfigureBehavior(_ => { }).BuildAsync());
        var shared = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(new MedianAdaptiveAverage());
        var actual = await Record.ExceptionAsync(() => shared.BuildAsync());
        Assert.NotNull(expected); Assert.NotNull(actual);
        Assert.Equal(expected.GetType(), actual.GetType()); Assert.Equal(expected.Message, actual.Message);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => shared.BuildAsync(cancellation.Token));
    }
}

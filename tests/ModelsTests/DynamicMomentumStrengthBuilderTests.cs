using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class DynamicMomentumStrengthBuilderTests
{
    private static Bar[] Prices(int count) => Enumerable.Range(0, count).Select(i =>
    {
        double price = 100 + (i * 13 % 101) / 8d;
        return new Bar(DateTime.UnixEpoch.AddDays(i), price, price, price, price, 0);
    }).ToArray();

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task BothHistoryModesRetainValuesPresenceAndChainedInputsAfterDisposal(bool latest, bool chained)
    {
        var bars = Prices(150);
        var indicator = new DynamicMomentumStrength(3, 5, 4, 8, 1);
        var average = new Sma(2);
        if (chained) indicator.Of(average);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(average, indicator)
            .ConfigureHistory(latest ? IndicatorHistoryMode.LatestOnly : IndicatorHistoryMode.Full)
            .BuildAsync();
        var inputs = chained ? run[average].ToArray() : bars.Select(b => b.Close).ToArray();
        var expected = DynamicMomentumSnapshot.FromValues(inputs.Select(p => (double?)p).ToArray(), 3, 5, 4, 8, 1);
        run.Dispose();
        Assert.Equal(expected.Select(v => BitConverter.DoubleToInt64Bits(v ?? 0)),
            run[indicator.Value].ToArray().Select(BitConverter.DoubleToInt64Bits));
        Assert.Equal(expected.Select(v => v.HasValue ? 1d : 0), run[indicator.IsDefined].ToArray());
    }

    [Theory]
    [InlineData(3, 5, 4, 8, 1)]
    [InlineData(5, 2, 14, 30, 5)]
    [InlineData(1, 1, 1, 1, 1)]
    [InlineData(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, 1)]
    [InlineData(2, 2, int.MaxValue, int.MaxValue, int.MaxValue)]
    public async Task LifecycleValidationCoversDifferentStartupsAndLazyExtremePeriods(int sd, int smooth, int rsi, int upper, int lower)
    {
        var report = await IndicatorValidation.ValidateAsync(new IndicatorValidationCase(typeof(DynamicMomentumStrength),
            "dynamic full-history strength", () => new DynamicMomentumStrength(sd, smooth, rsi, upper, lower)));
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void ReplayAfterResetDoesNotRetainOldPeriodStateOrAllocateTrajectories()
    {
        var bars = Prices(1000);
        var state = new DynamicMomentumStrengthState(3, 5, 4, 8, 1);
        var output = new double[2];
        void Replay() { foreach (var bar in bars) state.Update(bar, output); }
        for (int i = 0; i < 3; i++) { state.Reset(); Replay(); }
        var expected = output.ToArray();
        state.Reset();
        long before = GC.GetAllocatedBytesForCurrentThread();
        Replay();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(expected, output);
        state.Reset();
        foreach (var bar in Prices(80).Reverse()) state.Update(bar, output);
        var different = DynamicMomentumSnapshot.FromValues(Prices(80).Reverse().Select(b => (double?)b.Close).ToArray(), 3, 5, 4, 8, 1);
        Assert.Equal(different[^1] ?? 0, output[0]);
        Assert.Equal(different[^1].HasValue ? 1d : 0, output[1]);
    }

    [Fact]
    public void LatestOnlyUsesSharedCpuStorage()
    {
        var bars = Prices(1000);
        long Measure(IndicatorHistoryMode history)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            using var run = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(new DynamicMomentumStrength()).ConfigureHistory(history)
                .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            GC.KeepAlive(run);
            return allocated;
        }
        for (int i = 0; i < 3; i++) { Measure(IndicatorHistoryMode.Full); Measure(IndicatorHistoryMode.LatestOnly); }
        long full = Measure(IndicatorHistoryMode.Full), latest = Measure(IndicatorHistoryMode.LatestOnly);
        Assert.True(full - latest >= 40L * bars.Length, $"Full={full}, LatestOnly={latest}");
    }
}

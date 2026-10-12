using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class RelativeVolatilityStrengthBuilderTests
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
    public async Task BothHistoryModesPreserveChainedValuesAfterDisposal(bool latest, bool chained)
    {
        var bars = Prices(80);
        var indicator = new RelativeVolatilityStrength(3);
        var average = new Sma(2);
        if (chained) indicator.Of(average);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(average, indicator)
            .ConfigureHistory(latest ? IndicatorHistoryMode.LatestOnly : IndicatorHistoryMode.Full).BuildAsync();
        var inputs = chained ? run[average].ToArray() : bars.Select(b => b.Close).ToArray();
        var expected = RelativeVolatilitySnapshot.Calculate(inputs.Select(p => new Bar(default, p, p, p, p, 0)).ToArray(), 3);
        run.Dispose();
        Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), run[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(14)]
    [InlineData(int.MaxValue)]
    public async Task LifecycleAndLazyMaximumPeriodsMatchBatchReference(int period)
    {
        var report = await IndicatorValidation.ValidateAsync(new IndicatorValidationCase(typeof(RelativeVolatilityStrength),
            "relative volatility", () => new RelativeVolatilityStrength(period)));
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void ResetReplaysWithoutAllocatingOrdinaryArithmeticState()
    {
        var bars = Prices(1000);
        var state = new RelativeVolatilityStrength.State(14);
        double last = 0;
        void Replay() { foreach (var bar in bars) last = state.Update(bar); }
        for (int i = 0; i < 3; i++) { state.Reset(); Replay(); }
        double expected = last;
        state.Reset();
        long before = GC.GetAllocatedBytesForCurrentThread();
        Replay();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(expected, last);
        state.Reset();
        var reversed = bars.Take(40).Reverse().ToArray();
        var reference = RelativeVolatilitySnapshot.Calculate(reversed, 14);
        for (int i = 0; i < reversed.Length; i++)
            Assert.Equal(BitConverter.DoubleToInt64Bits(reference[i]), BitConverter.DoubleToInt64Bits(state.Update(reversed[i])));
    }

    [Fact]
    public void LatestOnlyUsesSharedCpuStorage()
    {
        var bars = Prices(1000);
        long Measure(IndicatorHistoryMode history)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            using var run = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(new RelativeVolatilityStrength()).ConfigureHistory(history)
                .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            GC.KeepAlive(run); return allocated;
        }
        for (int i = 0; i < 3; i++) { Measure(IndicatorHistoryMode.Full); Measure(IndicatorHistoryMode.LatestOnly); }
        long full = Measure(IndicatorHistoryMode.Full), latest = Measure(IndicatorHistoryMode.LatestOnly);
        Assert.True(full - latest >= 40L * bars.Length, $"Full={full}, LatestOnly={latest}");
    }
}

using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class ConnorsStrengthBuilderTests
{
    [Fact]
    public void LatestOnlyAvoidsFullBarStorageOnTheSharedCpuPath()
    {
        var bars = Enumerable.Range(0, 1000).Select(i =>
        {
            double price = 100 + (i * 13 % 101) / 8d;
            return new Bar(default, price, price, price, price, 0);
        }).ToArray();
        long Measure(IndicatorHistoryMode history)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            using var run = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(new ConnorsStrength()).ConfigureHistory(history)
                .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            GC.KeepAlive(run);
            return allocated;
        }
        for (int i = 0; i < 3; i++) { Measure(IndicatorHistoryMode.Full); Measure(IndicatorHistoryMode.LatestOnly); }
        long full = Measure(IndicatorHistoryMode.Full), latest = Measure(IndicatorHistoryMode.LatestOnly);
        Assert.True(full - latest >= 40L * bars.Length, $"Full={full}, LatestOnly={latest}");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task BothHistoryModesPreserveAllValuesPresenceAndChainedInputs(bool latest, bool chained)
    {
        double[] prices = [1, 2, 1, 3, 0, -1, 2, 4, 2, 1, 3, 5, 4, 6, 2, 3, 7, 4];
        var bars = prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddDays(i), p, p, p, p, 0)).ToArray();
        var indicator = new ConnorsStrength(2, 3, 3);
        var average = new Sma(2);
        if (chained) indicator.Of(average);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(average, indicator).ConfigureHistory(latest ? IndicatorHistoryMode.LatestOnly : IndicatorHistoryMode.Full)
            .BuildAsync();
        var inputs = chained ? run[average].ToArray() : prices;
        var expected = ConnorsStrengthReference.Calculate(inputs.Select((p, i) =>
            new Bar(bars[i].Time, p, p, p, p, 0)).ToArray(), 2, 3, 3);
        run.Dispose();
        for (int slot = 0; slot < 4; slot++)
        {
            var column = expected.Select(row => slot switch
                { 0 => row.Rsi, 1 => row.RsiStreak, 2 => row.PercentRank, _ => row.ConnorsRsi }).ToArray();
            Assert.Equal(column.Select(v => BitConverter.DoubleToInt64Bits(v ?? 0)),
                run[indicator.Outputs[slot]].ToArray().Select(BitConverter.DoubleToInt64Bits));
            Assert.Equal(column.Select(v => v.HasValue ? 1d : 0), run[indicator.Outputs[slot + 4]].ToArray());
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(int.MaxValue)]
    public async Task ValidationCoversLifecycleAndLazyMaximumPeriods(int period)
    {
        var report = await IndicatorValidation.ValidateAsync(new IndicatorValidationCase(typeof(ConnorsStrength),
            "inclusive Connors", () => new ConnorsStrength(period, period, period)));
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public void StateAllocatesNothingOnOrdinaryReplayIncludingStartupAfterReset()
    {
        var state = new ConnorsStrengthState(3, 2, 100);
        var output = new double[8];
        void Run()
        {
            for (int i = 0; i < 1000; i++)
            {
                double price = 100 + (i * 13 % 101) / 8d;
                state.Update(new Bar(default, price, price, price, price, 0), output);
            }
        }
        for (int i = 0; i < 3; i++) { state.Reset(); Run(); }
        var expected = output.ToArray();
        state.Reset();
        long before = GC.GetAllocatedBytesForCurrentThread();
        Run();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(expected, output);
    }
}

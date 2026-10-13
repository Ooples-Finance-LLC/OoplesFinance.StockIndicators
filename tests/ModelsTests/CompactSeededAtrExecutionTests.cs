using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class CompactSeededAtrExecutionTests
{
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(20)] [InlineData(int.MaxValue)]
    public void RoundedStagesMatchIndependentReferenceAcrossGridsAndReset(int period)
    {
        var state = new CompactSeededAtrWindow(period);
        foreach (int scale in new[] { -1074, -1070, -1022, -500, 0, 500, 1000 })
        {
            var bars = Enumerable.Range(0, 97).Select(i => new Bar(default, 0,
                Math.ScaleB(2 + (i * 7 % 103) / 4d, scale),
                Math.ScaleB((i % 11) / 4d, scale),
                Math.ScaleB((i % 13 - 6) / 8d, scale), 0)).ToArray();
            CheckState(state, bars, period);
            CheckState(state, bars.Reverse().ToArray(), period);
        }
        double m = double.MaxValue;
        Bar[] extreme = [new(default, 0, m, -m, m, 0), new(default, 0, m, -m, -m, 0),
            new(default, 0, -5, 7, 0, 0), new(default, 0, double.Epsilon, 0, -m, 0),
            new(default, 0, -0d, 0, -0d, 0), new(default, 0, 1, 1, -1, 0)];
        CheckState(state, extreme, period);
        CheckState(state, extreme.Reverse().ToArray(), period);
        CheckState(state, Enumerable.Repeat(new Bar(default, 0, 0, 0, -1, 0), 97).ToArray(), period);
    }

    [Theory]
    [InlineData(0, 20)] [InlineData(1, 20)] [InlineData(19, 20)]
    [InlineData(20, 20)] [InlineData(21, 20)] [InlineData(513, 20)]
    [InlineData(97, 1)] [InlineData(97, int.MaxValue)]
    public async Task FacadePreservesAllColumnsHistoryAndOwnership(int count, int period)
    {
        var random = new Random(914);
        var bars = Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i),
            i, 10 + random.NextDouble(), 8 + random.NextDouble(), i % 11 == 0 ? 0 : 9 + random.NextDouble(), i)).ToArray();
        var expected = SeededAtrReference.Values(bars, period);
        foreach (IIndicator indicator in new IIndicator[] { new SeededAverageTrueRange(period),
            new NormalizedSeededAverageTrueRange(period), new AverageTrueRangeWithDetails(period) })
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            Assert.True(ValuesBarExecution.SupportsOwned([indicator]));
            var input = bars.ToArray();
            var builder = Build(input, indicator).ConfigureHistory(history);
            using var run = await builder.BuildAsync();
            Assert.Equal(IndicatorExecutionBackend.Cpu, builder.LastExecution!.Backend);
            Array.Clear(input);
            run.Dispose();
            for (int slot = 0; slot < indicator.Outputs.Count; slot++)
            {
                int referenceSlot = indicator is AverageTrueRangeWithDetails ? slot
                    : indicator is NormalizedSeededAverageTrueRange ? 2 : 1;
                Assert.Equal(expected[referenceSlot].Select(BitConverter.DoubleToInt64Bits),
                    run[indicator.Outputs[slot]].ToArray().Select(BitConverter.DoubleToInt64Bits));
            }
            if (count > 0) Assert.Equal(bars[^1], run.Latest.Bar);
            if (history == IndicatorHistoryMode.Full)
            {
                int row = 0;
                await foreach (var snapshot in run) Assert.Equal(bars[row++], snapshot.Bar);
                Assert.Equal(count, row);
            }
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task InputValidationPrecedesOverflowAndBuildRecovers(bool details)
    {
        double m = double.MaxValue;
        var bars = Enumerable.Repeat(new Bar(default, 0, m, -m, m, 0), 5).ToArray();
        IIndicator indicator = details ? new AverageTrueRangeWithDetails(1) : new SeededAverageTrueRange(1);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            bars[1] = new(default, 0, m, -m, m, 0);
            var builder = Build(bars, indicator).ConfigureHistory(history);
            bars[^1] = new(default, 0, 0, 0, 0, double.NaN);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
            bars[^1] = new(default, 0, 0, 0, 0, 0);
            var error = await Assert.ThrowsAsync<IndicatorOutputException>(() => builder.BuildAsync());
            Assert.Equal(0, error.OutputSlot);
            Assert.Equal(1, error.BarIndex);
            Array.Fill(bars, new Bar(default, 0, 1, 0, 1, 0));
            using var recovered = await builder.BuildAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        }
    }

    [Fact]
    public async Task NormalizationRetainsUnpublishedOverflowAndNegativeUnderflowZero()
    {
        double m = double.MaxValue;
        Bar[][] cases = [
            [new(default, 0, m, -m, m, 0), new(default, 0, m, -m, m, 0), new(default, 0, m, -m, -m, 0)],
            [new(default, 0, 0, 0, 0, 0), new(default, 0, double.Epsilon, 0, -m, 0)],
            [new(default, 0, 0, 0, 0, 0), new(default, 0, 0, 0, -1, 0)]];
        foreach (var bars in cases)
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = new NormalizedSeededAverageTrueRange(1);
            using var run = await Build(bars, indicator).ConfigureHistory(history).BuildAsync();
            Assert.Equal(SeededAtrReference.Values(bars, 1)[2].Select(BitConverter.DoubleToInt64Bits),
                run[indicator.Value].ToArray().Select(BitConverter.DoubleToInt64Bits));
        }
    }

    [Fact]
    public void OrdinaryUpdatesAndPercentagesAllocateNoPerBarObjects()
    {
        var bars = Enumerable.Range(0, 97).Select(i => new Bar(default, 0,
            100 + i / 8d, 98 + i / 8d, 99 + i / 8d, 1)).ToArray();
        var state = new CompactSeededAtrWindow(20);
        for (int i = 0; i < 1000; i++) { state.Add(in bars[i % bars.Length]); state.Percent(bars[i % bars.Length].Close); }
        state.Reset();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 4000; i++) { state.Add(in bars[i % bars.Length]); state.Percent(bars[i % bars.Length].Close); }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(state.Average.Mean(1) > 0);
    }

    private static void CheckState(CompactSeededAtrWindow state, Bar[] bars, int period)
    {
        var expected = SeededAtrReference.Values(bars, period);
        state.Reset();
        for (int i = 0; i < bars.Length; i++)
        {
            state.Add(in bars[i]);
            Bits(expected[0][i], state.Range.Mean(1));
            Bits(expected[1][i], state.Average.Mean(1));
            Bits(expected[2][i], state.Percent(bars[i].Close));
            Assert.Equal(expected[3][i] != 0, state.HasRange);
            Assert.Equal(expected[4][i] != 0, state.HasAverage);
        }
    }

    private static StockIndicatorBuilder Build(Bar[] bars, IIndicator indicator) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).ConfigureExecution(IndicatorExecutionBackend.Cpu);
    private static void Bits(double expected, double actual) =>
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
}

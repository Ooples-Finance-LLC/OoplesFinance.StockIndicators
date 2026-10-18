using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class TillsonSharedExecutionTests(Xunit.Abstractions.ITestOutputHelper log)
{
    private static Bar[] BarsOf(IEnumerable<double> prices) => prices.Select((p, i) =>
        new Bar(DateTime.UnixEpoch.AddMinutes(i), i, i + 1, i - 1, p, i + 2)).ToArray();

    [Theory]
    [InlineData(TillsonSeed.FirstPrice)] [InlineData(TillsonSeed.FollowingPrefix)]
    [InlineData(TillsonSeed.CascadedMeans)]
    public async Task StartupSuppressionAndOwnedHistoriesMatchOriginal(TillsonSeed seed)
    {
        foreach (int period in new[] { 1, 3, 20, int.MaxValue })
        foreach (int count in new[] { 0, 1, 4, 13, 114, 115, 513 })
            await Check(BarsOf(Enumerable.Range(0, count).Select(i => 100 + (i * 13 % 101) / 8d)), period, .7, seed, 0);
        foreach (int suppression in new[] { 1, 7, int.MaxValue })
            await Check(BarsOf(Enumerable.Range(0, 65).Select(i => (double)(i % 7))), 3, .7, seed, suppression);
    }

    [Theory]
    [InlineData(TillsonSeed.FirstPrice)] [InlineData(TillsonSeed.FollowingPrefix)]
    [InlineData(TillsonSeed.CascadedMeans)]
    public async Task PolynomialCancellationSubnormalStagesAndOverflowMatchOriginal(TillsonSeed seed)
    {
        foreach (double factor in new[] { -1d, 0d, -.5, .7, double.Epsilon, double.MaxValue })
        {
            foreach (double price in new[] { -0d, double.Epsilon, double.MaxValue, -double.MaxValue })
                await Check(BarsOf(Enumerable.Repeat(price, 17)), 3, factor, seed, 0);
            await Check(BarsOf(Enumerable.Range(0, 35).Select(i => i % 2 == 0 ? double.MaxValue : -double.MaxValue)),
                3, factor, seed, 2);
        }
        foreach (int scale in new[] { -1074, -1022, -500, 0, 900 })
            await Check(BarsOf(Enumerable.Range(0, 65).Select(i => Math.ScaleB(i % 13 - 6d, scale))),
                3, .7, seed, 0);
    }

    [Theory]
    [InlineData(TillsonSeed.FirstPrice)] [InlineData(TillsonSeed.FollowingPrefix)]
    [InlineData(TillsonSeed.CascadedMeans)]
    public void ResetAndRandomExponentSpansPreserveEveryStageRounding(TillsonSeed seed)
    {
        var random = new Random(92733);
        foreach (double factor in new[] { .7, double.Epsilon, double.MaxValue })
        {
            var original = (IMultiOutputState)new TillsonAverage(3, factor, seed, 2).CreateState();
            var compact = new ValuesBarExecution.TillsonValueState(3, factor, seed, 2);
            var bars = BarsOf(Enumerable.Range(0, 65).Select(i =>
                Math.ScaleB(random.NextDouble() * 2 - 1, random.Next(-1074, 1024))));
            var expected = new double[2]; var actual = new double[2];
            for (int replay = 0; replay < 2; replay++)
            {
                original.Reset(); compact.Reset();
                foreach (var bar in bars)
                {
                    original.Update(bar, expected); compact.Update(bar, actual);
                    AssertBits(expected, actual);
                }
            }
        }
    }

    [Fact]
    public void OrdinaryStagesAvoidPaddedPerBarAllocations()
    {
        var bars = BarsOf(Enumerable.Range(0, 256).Select(i => 100 + (i * 13 % 101) / 8d));
        var original = (IMultiOutputState)new TillsonAverage(20).CreateState();
        var compact = new ValuesBarExecution.TillsonValueState(20, .7, TillsonSeed.FirstPrice, 0);
        var output = new double[2];
        long Measure(IMultiOutputState state)
        {
            state.Reset();
            long before = GC.GetAllocatedBytesForCurrentThread();
            foreach (var bar in bars) state.Update(bar, output);
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
        Measure(original); Measure(compact);
        long oldBytes = Measure(original), newBytes = Measure(compact);
        log.WriteLine($"256 updates: original {oldBytes} bytes, compact {newBytes} bytes.");
        Assert.True(newBytes < oldBytes / 2, $"Compact {newBytes}, original {oldBytes}");
    }

    [Fact]
    public async Task MixedDuplicateRootsRetainIndependentState()
    {
        var bars = BarsOf(Enumerable.Range(0, 65).Select(i => 100 + i % 7d));
        var first = new TillsonAverage(3);
        var second = new TillsonAverage(7, -.5, TillsonSeed.CascadedMeans, 2);
        var engine = new CustomIndicatorEngine(bars, _ => null);
        var a = engine.Compute(first); var b = engine.Compute(second);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(first, second, first).ConfigureHistory(history).BuildAsync();
            for (int slot = 0; slot < 2; slot++)
            {
                AssertBits(a[slot], run[first.Outputs[slot]].ToArray());
                AssertBits(b[slot], run[second.Outputs[slot]].ToArray());
            }
        }
    }

    [Fact]
    public async Task InputValidationPrecedesPolynomialFailureAndBackendRecovers()
    {
        var bars = BarsOf([1d, -1, 1]);
        bars[^1] = new(default, 1, 1, 1, 1, double.NaN);
        var indicator = new TillsonAverage(3, double.MaxValue);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
        for (int i = 0; i < bars.Length; i++) bars[i] = new(default, 1, 1, 1, 1, 1);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        using var run = await builder.ConfigureExecution(IndicatorExecutionBackend.Auto).BuildAsync();
        Assert.Equal(IndicatorExecutionBackend.Cpu, builder.LastExecution!.Backend);
        Assert.All(run[indicator.Value].ToArray(), v => Assert.Equal(1, v));
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
    }

    private static async Task Check(Bar[] bars, int period, double factor, TillsonSeed seed, int suppression)
    {
        var indicator = new TillsonAverage(period, factor, seed, suppression);
        var state = (IMultiOutputState)indicator.CreateState();
        var expected = new[] { new double[bars.Length], new double[bars.Length] };
        var scratch = new double[2];
        for (int i = 0; i < bars.Length; i++)
        {
            state.Update(bars[i], scratch);
            for (int slot = 0; slot < 2; slot++) expected[slot][i] = scratch[slot];
        }
        int failure = Array.FindIndex(expected[0], v => !double.IsFinite(v));
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var input = bars.ToArray();
            var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(input))
                .ConfigureIndicators(indicator).ConfigureHistory(mode);
            if (failure >= 0)
            {
                var exception = await Assert.ThrowsAsync<IndicatorOutputException>(() => builder.BuildAsync());
                Assert.Equal(0, exception.OutputSlot);
                Assert.Equal(failure, exception.BarIndex);
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected[0][failure]), BitConverter.DoubleToInt64Bits(exception.Value));
                continue;
            }
            using var run = await builder.BuildAsync();
            Array.Clear(input); run.Dispose();
            for (int slot = 0; slot < 2; slot++) AssertBits(expected[slot], run[indicator.Outputs[slot]].ToArray());
            if (bars.Length > 0) Assert.Equal(bars[^1], run.Latest.Bar);
            if (mode == IndicatorHistoryMode.Full)
            {
                int row = 0;
                await foreach (var snapshot in run) Assert.Equal(bars[row++], snapshot.Bar);
                Assert.Equal(bars.Length, row);
            }
        }
    }
    private static void AssertBits(double[] expected, double[] actual) =>
        Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
}

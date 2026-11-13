using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class TillsonCheckedRegionTests
{
    [Fact]
    public void ContinuationKeysRequireCompletedSeedsAndAllRelevantState()
    {
        var full = new ValuesBarExecution.TillsonValueState(20, .7, TillsonSeed.CascadedMeans, 0);
        Assert.False(full.CaptureContinuation().IsSeeded);
        Assert.False(full.TrySetContinuationIndex(1249));
        var scratch = new double[2];
        var plateau = Plateau(1250, 20);
        foreach (var bar in plateau) full.Update(in bar, scratch);
        Assert.False(full.TrySetContinuationIndex(1248));
        var restarted = new ValuesBarExecution.TillsonValueState(20, .7, TillsonSeed.CascadedMeans, 0);
        foreach (var bar in plateau.AsSpan(610)) restarted.Update(in bar, scratch);
        Assert.True(restarted.TrySetContinuationIndex(1249));
        Assert.True(full.CaptureContinuation().IsSeeded);
        Assert.NotEqual(full.CaptureContinuation(), restarted.CaptureContinuation());

        ValuesBarExecution.TillsonContinuation ConstantKey(int period, double factor, int suppression)
        {
            var state = new ValuesBarExecution.TillsonValueState(period, factor, TillsonSeed.CascadedMeans, suppression);
            var bar = new Bar(default, 1, 1, 1, 1, 1);
            for (int i = 0; i < 640; i++) state.Update(in bar, scratch);
            Assert.True(state.TrySetContinuationIndex(1249));
            return state.CaptureContinuation();
        }
        var key = ConstantKey(20, .7, 0);
        Assert.Equal(key, ConstantKey(20, .7, 0));
        // Both configurations start publishing at114, but their future means differ.
        Assert.NotEqual(key, ConstantKey(2, .7, 108));
        Assert.NotEqual(key, ConstantKey(20, .5, 0));
        Assert.NotEqual(key, ConstantKey(20, .7, 5000));
        foreach (var seed in new[] { TillsonSeed.FirstPrice, TillsonSeed.FollowingPrefix })
        {
            var state = new ValuesBarExecution.TillsonValueState(20, .7, seed, 0);
            var bar = new Bar(default, 1, 1, 1, 1, 1);
            for (int i = 0; i < 640; i++) state.Update(in bar, scratch);
            Assert.False(state.CaptureContinuation().IsSeeded);
            Assert.False(state.TrySetContinuationIndex(1249));
        }
    }

    [Fact]
    public async Task RaggedThresholdsSuppressionAndFallbacksMatchOriginalState()
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int count in new[] { 8191, 8192, 10003 })
            foreach (int period in new[] { 2, 20, 40 })
            {
                var bars = Rich(count);
                var owner = new TillsonAverage(period, .7, TillsonSeed.CascadedMeans);
                var expected = Reference(bars, owner);
                foreach (int workers in new[] { 1, 8 })
                {
                    CpuParallelSettings.MaxDegreeOfParallelism = workers;
                    await Check(bars, owner, expected);
                }
            }
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            foreach (var seed in Enum.GetValues<TillsonSeed>())
            foreach (int period in new[] { 1, int.MaxValue })
            {
                var bars = Rich(8192);
                var owner = new TillsonAverage(period, .7, seed, 7);
                await Check(bars, owner, Reference(bars, owner));
            }
            var suppressed = new TillsonAverage(20, .7, TillsonSeed.CascadedMeans, 5000);
            var ragged = Rich(10003);
            await Check(ragged, suppressed, Reference(ragged, suppressed));
            double[] extremes = [-0d, double.Epsilon, -double.Epsilon, Math.ScaleB(1d, -1022),
                Math.ScaleB(1d, 900), Math.ScaleB(-1d, 900)];
            var wide = Enumerable.Range(0, 10003).Select(i =>
                new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, 1, -1, extremes[i % extremes.Length], i + 1)).ToArray();
            var wideOwner = new TillsonAverage(20, .7, TillsonSeed.CascadedMeans, 7);
            await Check(wide, wideOwner, Reference(wide, wideOwner));
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Fact]
    public async Task PlateauRepairsAndConcurrentBuildsPreserveExactOutputs()
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            var bars = Plateau(10003, 20);
            foreach (int suppression in new[] { 0, 5000 })
            {
                var owner = new TillsonAverage(20, .7, TillsonSeed.CascadedMeans, suppression);
                var expected = Reference(bars, owner);
                int firstDefined = Array.FindIndex(expected[1], value => value == 1);
                Assert.True(firstDefined >= 0);
                Assert.All(expected[0].Skip(firstDefined), value => Assert.Equal(1d, value));
                await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Task.Run(() => Check(bars, owner, expected))));
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Fact]
    public async Task InputErrorsPrecedeDeferredOverflowAndSameBuilderRecovers()
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                var bars = Enumerable.Range(0, 10003).Select(i =>
                    new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, 1, -1, i % 2 == 0 ? 1 : -1, 1)).ToArray();
                var owner = new TillsonAverage(2, double.MaxValue, TillsonSeed.CascadedMeans);
                var expected = Reference(bars, owner);
                int firstFailure = Array.FindIndex(expected[0], x => !double.IsFinite(x));
                Assert.True(firstFailure >= 0);
                var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner)
                    .ConfigureHistory(history).ConfigureExecution(IndicatorExecutionBackend.Cpu);
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
                var last = bars[^1];
                bars[^1] = new(last.Time, last.Open, last.High, last.Low, last.Close, double.NaN);
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
                bars[^1] = last;
                var error = await Assert.ThrowsAsync<IndicatorOutputException>(() => builder.BuildAsync());
                Assert.Equal(0, error.OutputSlot);
                Assert.Equal(firstFailure, error.BarIndex);
                for (int i = 0; i < bars.Length; i++) bars[i] = new(bars[i].Time, 1, 1, 1, 1, 1);
                using var recovered = await builder.BuildAsync();
                AssertColumns(recovered, owner, Reference(bars, owner));
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    private static Bar[] Rich(int count) => Enumerable.Range(0, count).Select(i =>
    {
        double close = 100 + (i * 13 % 101) / 8d;
        return new Bar(DateTime.UnixEpoch.AddMinutes(i), close - 1, close + 2, close - 2, close, i + 1);
    }).ToArray();

    private static Bar[] Plateau(int count, int period) => Enumerable.Range(0, count).Select(i =>
        new Bar(DateTime.UnixEpoch.AddMinutes(i), 1, 2, 0,
            i <= 6 * (period - 1) ? 1 : Math.BitIncrement(1d), 1)).ToArray();

    private static double[][] Reference(Bar[] bars, TillsonAverage owner)
    {
        var state = (IMultiOutputState)owner.CreateState();
        var result = new[] { new double[bars.Length], new double[bars.Length] };
        var scratch = new double[2];
        for (int i = 0; i < bars.Length; i++)
        {
            state.Update(in bars[i], scratch);
            for (int slot = 0; slot < 2; slot++) result[slot][i] = scratch[slot];
        }
        return result;
    }

    private static async Task Check(Bar[] bars, TillsonAverage owner, double[][] expected)
    {
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var input = bars.ToArray();
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input)).ConfigureIndicators(owner)
                .ConfigureHistory(history).ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
            Array.Clear(input);
            run.Dispose();
            AssertColumns(run, owner, expected);
            Assert.Equal(bars[^1], run.Latest.Bar);
            if (history == IndicatorHistoryMode.Full && bars.Length == 10003 && owner.Period == 20)
            {
                int row = 0;
                await foreach (var snapshot in run) Assert.Equal(bars[row++], snapshot.Bar);
                Assert.Equal(bars.Length, row);
            }
        }
    }

    private static void AssertColumns(IIndicatorRun run, TillsonAverage owner, double[][] expected)
    {
        for (int slot = 0; slot < 2; slot++)
            Assert.Equal(expected[slot].Select(BitConverter.DoubleToInt64Bits),
                run[owner.Outputs[slot]].ToArray().Select(BitConverter.DoubleToInt64Bits));
    }
}

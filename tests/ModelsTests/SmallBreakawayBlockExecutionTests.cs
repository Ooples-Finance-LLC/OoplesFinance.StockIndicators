using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class SmallBreakawayBlockExecutionTests
{
    private static Bar[] Data(int count, bool dense = false, bool mirror = false)
    {
        Bar[] pattern = [new(default, 20, 21, 9, 10, 1), new(default, 9, 10, 7, 8, 1),
            new(default, 8, 9, 6, 7.5, 1), new(default, 7, 8, 5, 6.5, 1), new(default, 8, 10, 7, 9.5, 1)];
        var bars = Enumerable.Range(0, count).Select(i =>
            new Bar(DateTime.UnixEpoch.AddMinutes(i), 10, 10.2, 9.8, 9.9, i + 1)).ToArray();
        var ends = dense ? Enumerable.Range(0, count / 32).Select(i => i * 32 + 31)
            : new[] { 79, count / 2 - 1, count / 2, count / 2 + 4, count - 1 };
        foreach (int end in ends)
            if (end >= 4 && end < count)
                for (int j = 0; j < pattern.Length; j++)
                {
                    int row = end - 4 + j;var b = pattern[j];
                    bars[row] = new(bars[row].Time, b.Open, b.High, b.Low, b.Close, row + 1);
                }
        if (mirror)
            for (int i = 0; i < bars.Length; i++)
            {
                var b = bars[i];
                bars[i] = new(b.Time, -b.Open, -b.Low, -b.High, -b.Close, b.Volume);
            }
        return bars;
    }

    private static async Task<double[]> Check(Bar[] bars, BreakawayCandle owner, int[] degrees)
    {
        var expected = ExtendedReversalReference.Evaluate(bars, owner.Period,
            ExtendedReversalKind.Breakaway).ToArray();
        var original = (IIndicatorState)owner.CreateState();
        Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits),
            bars.Select(b => BitConverter.DoubleToInt64Bits(original.Update(in b))));
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int workers in degrees)
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
            {
                CpuParallelSettings.MaxDegreeOfParallelism = workers;
                var input = bars.ToArray();
                using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input))
                    .ConfigureIndicators(owner).ConfigureHistory(mode).BuildAsync();
                Array.Clear(input);run.Dispose();
                Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits),
                    run[owner].ToArray().Select(BitConverter.DoubleToInt64Bits));
                Assert.Equal(bars[^1], run.Latest.Bar);
                if (mode == IndicatorHistoryMode.Full)
                {
                    int row = 0;
                    await foreach (var snapshot in run) Assert.Equal(bars[row++], snapshot.Bar);
                    Assert.Equal(bars.Length, row);
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
        return expected;
    }

    [Theory]
    [InlineData(511)] [InlineData(512)] [InlineData(513)]
    [InlineData(1000)] [InlineData(8191)] [InlineData(8192)]
    public async Task SmallRouteBoundariesAndWorkerTailAnchorsMatchIndependentReference(int count)
    {
        var expected = await Check(Data(count), new BreakawayCandle(), [1, 2]);
        Assert.Contains(100d, expected);
        if (count == 1000) await Check(Data(count), new BreakawayCandle(65), [2]);
    }

    [Theory]
    [InlineData(1)] [InlineData(64)]
    public async Task DenseEscapeBothDirectionsAndWideHaloKeepExactOutputs(int period)
    {
        foreach (bool mirror in new[] { false, true })
        {
            var expected = await Check(Data(1025, dense: true, mirror: mirror), new BreakawayCandle(period), [2, 8]);
            Assert.Contains(mirror ? -100d : 100d, expected);
        }
    }

    [Fact]
    public async Task SkippedBlocksValidateAllFieldsAndRecoverAfterCancelledAndInvalidBuilds()
    {
        var bars = Enumerable.Range(0, 1001).Select(i =>
            new Bar(DateTime.UnixEpoch.AddMinutes(i), 5, 10, 0, 5, 1)).ToArray();
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 2;
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
            {
                var owner = new BreakawayCandle(64);
                var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                    .ConfigureIndicators(owner).ConfigureHistory(mode);
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
                int[] rows = [0, 31, 32, 499, 500];
                for (int field = 0; field < rows.Length; field++)
                {
                    int row = rows[field];var valid = bars[row];
                    bars[row] = new(valid.Time, field == 0 ? double.NaN : valid.Open,
                        field == 1 ? double.PositiveInfinity : valid.High,
                        field == 2 ? double.NegativeInfinity : valid.Low,
                        field == 3 ? double.NaN : valid.Close,
                        field == 4 ? double.NaN : valid.Volume);
                    await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
                    bars[row] = valid;
                }
                using var run = await builder.BuildAsync();
                Assert.All(run[owner].ToArray(), value => Assert.Equal(0L, BitConverter.DoubleToInt64Bits(value)));
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }
}

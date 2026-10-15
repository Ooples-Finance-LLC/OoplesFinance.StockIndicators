using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class PivotSharedExecutionTests
{
    [Theory]
    [InlineData(PivotLevelStyle.Standard, 9)]
    [InlineData(PivotLevelStyle.Camarilla, 10)]
    [InlineData(PivotLevelStyle.Demark, 5)]
    [InlineData(PivotLevelStyle.Fibonacci, 9)]
    [InlineData(PivotLevelStyle.Woodie, 9)]
    public async Task IdenticalColumnsShareStorageOnlyWithinTheirOwnRun(PivotLevelStyle style, int columnCount)
    {
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        foreach (bool mixed in new[] { false, true })
        {
            var original = BarsOf(513, .125);
            var input = original.ToArray();
            var pivot = new RollingPivotLevels(3, 2, style);
            var other = new RollingPivotLevels(7, 1, style);
            var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(input))
                .ConfigureIndicators(mixed ? [pivot, other] : [pivot]).ConfigureHistory(mode);
            using var first = await builder.BuildAsync();
            Assert.Equal(columnCount, DistinctColumnCount(first, pivot));
            AssertColumns(original, pivot, first);
            if (mixed)
            {
                AssertColumns(original, other, first);
                AssertDisjointColumns(first, pivot, first, other);
            }
            input[0] = new(default, 2, 3, 1, 2, 1);
            using var second = await builder.BuildAsync();
            AssertDisjointColumns(first, pivot, second, pivot);
            AssertColumns(input, pivot, second);
            Array.Clear(input);
            first.Dispose();
            AssertColumns(original, pivot, first);
        }
    }

    private static int DistinctColumnCount(IIndicatorRun run, RollingPivotLevels pivot)
    {
        int count = 0;
        for (int slot = 0; slot < pivot.Outputs.Count; slot++)
        {
            bool shared = false;
            for (int prior = 0; prior < slot; prior++)
                shared |= run[pivot.Outputs[slot]].Overlaps(run[pivot.Outputs[prior]]);
            if (!shared) count++;
        }
        return count;
    }

    private static void AssertDisjointColumns(IIndicatorRun first, RollingPivotLevels left,
        IIndicatorRun second, RollingPivotLevels right)
    {
        foreach (var a in left.Outputs)
        foreach (var b in right.Outputs)
            Assert.False(first[a].Overlaps(second[b]));
    }

    [Theory]
    [InlineData(PivotLevelStyle.Standard)] [InlineData(PivotLevelStyle.Camarilla)]
    [InlineData(PivotLevelStyle.Demark)] [InlineData(PivotLevelStyle.Fibonacci)] [InlineData(PivotLevelStyle.Woodie)]
    public async Task WorkerReplayPreservesOffsetsAndEveryColumn(PivotLevelStyle style)
    {
        int previous = AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int workers in new[] { 1, 8 })
            {
                AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism = workers;
                foreach (var (period, offset, count) in new[] { (20, 7, 1001), (1, 0, 513), (511, 1, 2051), (4095, 0, 8193), (4096, 0, 8193) })
                    await Check(BarsOf(count, .125), new(period, offset, style));
            }
        }
        finally { AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism = previous; }
    }

    [Theory]
    [InlineData(PivotLevelStyle.Standard, 2)]
    [InlineData(PivotLevelStyle.Camarilla, 4)]
    [InlineData(PivotLevelStyle.Demark, 5)]
    [InlineData(PivotLevelStyle.Fibonacci, 2)]
    [InlineData(PivotLevelStyle.Woodie, 2)]
    public async Task LaterWorkerInvalidInputPrecedesEarlierSecondaryOverflow(PivotLevelStyle style, int expectedSlot)
    {
        int previous = AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism = 8;
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
            {
                var bars = BarsOf(1025, 1);
                bars[0] = new(default, 0, double.MaxValue, -double.MaxValue,
                    style == PivotLevelStyle.Demark ? double.MaxValue : 0, 1);
                bars[^1] = new(default, 0, 1, -1, 0, double.NaN);
                var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                    .ConfigureIndicators(new RollingPivotLevels(1, style: style)).ConfigureHistory(mode);
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
                Assert.Null(builder.LastExecution);
                bars[^1] = new(default, 0, 1, -1, 0, 1);
                var error = await Assert.ThrowsAsync<IndicatorOutputException>(() => builder.BuildAsync());
                Assert.Equal(1, error.BarIndex);
                Assert.Equal(expectedSlot, error.OutputSlot);
                Assert.Null(builder.LastExecution);
            }
        }
        finally { AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism = previous; }
    }

    [Theory]
    [InlineData(PivotLevelStyle.Standard)] [InlineData(PivotLevelStyle.Camarilla)]
    [InlineData(PivotLevelStyle.Demark)] [InlineData(PivotLevelStyle.Fibonacci)] [InlineData(PivotLevelStyle.Woodie)]
    public async Task EveryStyleWindowAndOffsetRetainsAllOwnedColumns(PivotLevelStyle style)
    {
        foreach (var (period, offset) in new[] { (1, 0), (3, 2), (20, 0), (int.MaxValue, 0), (1, int.MaxValue - 1) })
        foreach (int count in new[] { 0, 1, 2, 5, 6, 20, 21, 65 })
            await Check(BarsOf(count, 1), new(period, offset, style));
    }
    [Fact]
    public async Task WideSubnormalAndSignedZeroLevelsMatchSnapshots()
    {
        int previous = AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int workers in new[] { 1, 8 })
            {
                AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism = workers;
                foreach (double scale in new[] { double.MaxValue / 256, double.Epsilon, 1e-200, -0d })
                foreach (var style in Enum.GetValues<PivotLevelStyle>())
                foreach (int count in new[] { 65, 513 })
                    await Check(BarsOf(count, scale), new(3, 2, style));
            }
        }
        finally { AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism = previous; }
    }
    [Fact]
    public async Task ValidationPrecedesOverflowAndMixedRootsRecover()
    {
        var bars = BarsOf(65, 1); var first = new RollingPivotLevels(3); var second = new RollingPivotLevels(5, 2, PivotLevelStyle.Woodie);
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(first, second, first).ConfigureHistory(mode);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
            using var run = await builder.ConfigureExecution(IndicatorExecutionBackend.Auto).BuildAsync();
            Assert.Equal(IndicatorExecutionBackend.Cpu, builder.LastExecution!.Backend);
            AssertColumns(bars, first, run); AssertColumns(bars, second, run);
            var overflow = new[] { new Bar(default, 0, double.MaxValue, -double.MaxValue, double.MaxValue, 1), new Bar(default, 1, 1, 1, 1, 1), new Bar(default, 1, 1, 1, 1, double.NaN) };
            var one = new RollingPivotLevels(1);
            var invalid = new StockIndicatorBuilder().ConfigureSource(Bars.From(overflow)).ConfigureIndicators(one).ConfigureHistory(mode);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => invalid.BuildAsync());
            overflow[2] = new(default, 1, 1, 1, 1, 1);
            var error = await Assert.ThrowsAsync<IndicatorOutputException>(() => invalid.BuildAsync());
            Assert.Equal(1, error.BarIndex); Assert.Equal(2, error.OutputSlot);
        }
        await Assert.ThrowsAsync<NotSupportedException>(() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(first).ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
    }
    private static Bar[] BarsOf(int count, double scale) => Enumerable.Range(0, count)
        .Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), (i % 3 - 1) * scale,
            (i % 7 + 2) * scale, (i % 5 - 6) * scale, (i % 3 - 1) * scale, i + 1)).ToArray();
    private static async Task Check(Bar[] bars, RollingPivotLevels indicator)
    {
        Assert.True(ValuesBarExecution.SupportsOwned([indicator]));
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var input = bars.ToArray();
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input))
                .ConfigureIndicators(indicator).ConfigureHistory(mode).BuildAsync();
            Array.Clear(input); run.Dispose(); AssertColumns(bars, indicator, run);
            if (bars.Length > 0) Assert.Equal(bars[^1], run.Latest.Bar);
            if (mode == IndicatorHistoryMode.Full)
            {
                int row = 0; await foreach (var snapshot in run) Assert.Equal(bars[row++], snapshot.Bar);
                Assert.Equal(bars.Length, row);
            }
        }
    }
    private static void AssertColumns(Bar[] bars, RollingPivotLevels indicator, IIndicatorRun run)
    {
        var rows = PivotLevelSnapshots.Rolling(bars, indicator.Period, indicator.Offset, indicator.Style)
            .Select(v => new[] { v.PP, v.S1, v.S2, v.S3, v.S4, v.R1, v.R2, v.R3, v.R4 }).ToArray();
        for (int slot = 0; slot < 9; slot++)
        {
            Assert.Equal(rows.Select(r => BitConverter.DoubleToInt64Bits(r[slot] ?? 0)),
                run[indicator.Outputs[slot]].ToArray().Select(BitConverter.DoubleToInt64Bits));
            Assert.Equal(rows.Select(r => r[slot].HasValue ? 1d : 0), run[indicator.Outputs[slot + 9]].ToArray());
        }
    }
}

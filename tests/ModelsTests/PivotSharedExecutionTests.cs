using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class PivotSharedExecutionTests
{
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
        foreach (double scale in new[] { double.MaxValue / 256, double.Epsilon, 1e-200, -0d })
        foreach (var style in Enum.GetValues<PivotLevelStyle>())
            await Check(BarsOf(65, scale), new(3, 2, style));
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

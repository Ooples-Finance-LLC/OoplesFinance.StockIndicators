using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class SeededPhaseSharedExecutionTests
{
    private static Bar[] BarsOf(IEnumerable<double> prices) => prices.Select((p, i) =>
        new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p + 2, p - 1, p, i + 2)).ToArray();

    [Theory]
    [InlineData(false, false, false)] [InlineData(false, false, true)]
    [InlineData(false, true, false)] [InlineData(false, true, true)]
    [InlineData(true, false, false)] [InlineData(true, false, true)]
    [InlineData(true, true, false)] [InlineData(true, true, true)]
    public async Task SixPriceStartupPhaseModesAndOwnedColumnsMatchState(bool publish, bool older, bool midpoint)
    {
        foreach (int count in new[] { 0, 1, 5, 6, 7, 33, 129 })
        foreach (var limits in new[] { (.5, .05), (.2, .2), (1d, double.Epsilon) })
            await Check(BarsOf(Enumerable.Range(0, count).Select(i => i % 53 < 12 ? 100 : 100 + (i * 13 % 101) / 8d)),
                new(limits.Item1, limits.Item2, publish, older, midpoint));
    }

    [Fact]
    public async Task WideMidpointsAndSubnormalStagesSurviveOwnershipBoundaries()
    {
        foreach (double[] prices in new double[][] {
            [double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon, 0, -0d],
            [double.MaxValue, double.MaxValue, double.MaxValue],
            [-0d, 0d, -double.Epsilon, double.Epsilon] })
        foreach (bool midpoint in new[] { false, true })
        {
            var bars = Enumerable.Range(0, 33).Select(i =>
            {
                double p = prices[i % prices.Length];
                return new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, midpoint ? -p : p, p, 0);
            }).ToArray();
            await Check(bars, new(.5, .05, true, true, midpoint));
        }
    }

    [Fact]
    public async Task MixedDuplicateMappingsRetainIndependentState()
    {
        var bars = BarsOf(Enumerable.Range(0, 65).Select(i => 100 + i % 7d));
        var first = new SeededPhaseAdaptiveAverage(.5, .05, true, true);
        var second = new SeededPhaseAdaptiveAverage(.5, .05, false, false, true);
        var engine = new CustomIndicatorEngine(bars, _ => null);
        var a = engine.Compute(first); var b = engine.Compute(second);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(first, second, first).ConfigureHistory(history).BuildAsync();
            for (int slot = 0; slot < 4; slot++)
            {
                AssertBits(a[slot], run[first.Outputs[slot]].ToArray());
                AssertBits(b[slot], run[second.Outputs[slot]].ToArray());
            }
        }
    }

    [Fact]
    public async Task ValidationCancellationAndBackendContractsRecover()
    {
        var bars = BarsOf(Enumerable.Repeat(1d, 33));
        var indicator = new SeededPhaseAdaptiveAverage();
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator);
        bars[^1] = new(default, 1, 1, 1, 1, double.NaN);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
        bars[^1] = new(default, 1, 1, 1, 1, 1);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        using var run = await builder.ConfigureExecution(IndicatorExecutionBackend.Auto).BuildAsync();
        Assert.Equal(IndicatorExecutionBackend.Cpu, builder.LastExecution!.Backend);
        Assert.Equal(1, run[indicator.Mama].ToArray()[^1]);
        Assert.Equal(1, run[indicator.Fama].ToArray()[^1]);
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
    }

    private static async Task Check(Bar[] bars, SeededPhaseAdaptiveAverage indicator)
    {
        var state = (IMultiOutputState)indicator.CreateState();
        var expected = Enumerable.Range(0, 4).Select(_ => new double[bars.Length]).ToArray();
        var scratch = new double[4];
        for (int i = 0; i < bars.Length; i++)
        {
            state.Update(bars[i], scratch);
            for (int slot = 0; slot < 4; slot++) expected[slot][i] = scratch[slot];
        }
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var input = bars.ToArray();
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input))
                .ConfigureIndicators(indicator).ConfigureHistory(mode).BuildAsync();
            Array.Clear(input); run.Dispose();
            for (int slot = 0; slot < 4; slot++) AssertBits(expected[slot], run[indicator.Outputs[slot]].ToArray());
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

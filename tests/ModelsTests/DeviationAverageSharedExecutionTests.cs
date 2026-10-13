using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class DeviationAverageSharedExecutionTests
{
    [Theory]
    [InlineData(1, 0)] [InlineData(1, 1)] [InlineData(1, 513)] [InlineData(2, 513)]
    [InlineData(3, 513)] [InlineData(7, 1025)] [InlineData(20, 19)] [InlineData(20, 20)]
    [InlineData(20, 21)] [InlineData(20, 22)] [InlineData(20, 513)]
    [InlineData(256, 513)] [InlineData(int.MaxValue, 33)]
    public async Task StartupExpirationAndRoundedFilterMatchOriginalState(int period, int count)
    {
        var random = new Random(4731);
        var bars = Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), i,
            i + 1, i - 1, i % 89 < 31 ? 3 : random.NextDouble() * 23 - 10, i + 2)).ToArray();
        await Check(bars, period, .9);
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(7)] [InlineData(20)]
    [InlineData(256)] [InlineData(int.MaxValue)]
    public async Task SubnormalAndUnpublishedOverflowBeforeNormalizationRemainExact(int period)
    {
        foreach (double scale in new[] { .9, 1, 1e-100, double.Epsilon })
        {
            foreach (int grid in new[] { -1074, -1022, -500, 0, 900 })
                await Check(Enumerable.Range(0, 65).Select(i => new Bar(default, 0, 0, 0,
                    Math.ScaleB((i * 13 % 23) - 11, grid), 0)).ToArray(), period, scale);
            double[] extremes = [double.MaxValue, -double.MaxValue, double.MaxValue / 2,
                -double.MaxValue / 2, double.Epsilon, -double.Epsilon, 0d, -0d];
            await Check(Enumerable.Range(0, 65).Select(i => new Bar(default, 0, 0, 0,
                extremes[i % extremes.Length], 0)).ToArray(), period, scale);
        }
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(7)] [InlineData(20)]
    public async Task SquareGridChangesAndZeroWindowsMatchOriginalState(int period)
    {
        // Large squares expire, tiny squares introduce a lower grid, and a
        // long zero tail exercises a zero sum followed by a new nonzero term.
        double[] prices = [.. Enumerable.Repeat(0d, 25), double.MaxValue, -double.MaxValue,
            .. Enumerable.Repeat(0d, 1200), double.Epsilon, -double.Epsilon,
            .. Enumerable.Repeat(0d, 25), 1e-200, -1e-200, 1, -1];
        await Check(prices.Select(v => new Bar(default, 0, 0, 0, v, 0)).ToArray(), period, 1);
    }

    [Fact]
    public async Task SignedZeroConstantWindowsAndDuplicateMixedRootsRemainExact()
    {
        foreach (double price in new[] { 0d, -0d, double.Epsilon, double.MaxValue, -double.MaxValue })
            await Check(Enumerable.Repeat(new Bar(default, 0, 0, 0, price, 0), 65).ToArray(), 7, .9);
        var bars = Enumerable.Range(0, 65).Select(i => new Bar(default, 0, 0, 0, i % 11, 0)).ToArray();
        var first = new FilteredDeviationAverage(3, 1); var second = new FilteredDeviationAverage(7);
        var fractal = new RangeFractalAverage(7);
        var engine = new CustomIndicatorEngine(bars, _ => null);
        var roots = new IndicatorBase[] { first, second, fractal };
        var expected = roots.Select(root => engine.Compute(root)[0]).ToArray();
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(first, second, first, fractal).ConfigureHistory(mode).BuildAsync();
            for (int i = 0; i < roots.Length; i++)
                Assert.Equal(expected[i].Select(BitConverter.DoubleToInt64Bits),
                    run[roots[i].Outputs[0]].ToArray().Select(BitConverter.DoubleToInt64Bits));
        }
    }

    [Fact]
    public async Task ValidationCancellationRecoveryAndBackendContractsRemainIntact()
    {
        var bars = Enumerable.Repeat(new Bar(default, 0, 0, 0, 1, 0), 513).ToArray();
        var indicator = new FilteredDeviationAverage(20);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator);
        bars[^1] = new(default, double.NaN, 0, 0, 1, 0);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
        bars[^1] = new(default, 0, 0, 0, 1, 0);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        using var run = await builder.ConfigureExecution(IndicatorExecutionBackend.Auto).BuildAsync();
        Assert.Equal(IndicatorExecutionBackend.Cpu, builder.LastExecution!.Backend);
        Assert.All(run[indicator.Value].ToArray(), x => Assert.Equal(1, x));
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
    }

    private static async Task Check(Bar[] bars, int period, double scale)
    {
        var indicator = new FilteredDeviationAverage(period, scale);
        var expected = new CustomIndicatorEngine(bars, _ => null).Compute(indicator)[0];
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var input = bars.ToArray();
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input))
                .ConfigureIndicators(indicator).ConfigureHistory(mode).BuildAsync();
            Array.Clear(input); run.Dispose();
            Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits),
                run[indicator.Value].ToArray().Select(BitConverter.DoubleToInt64Bits));
            if (bars.Length > 0) Assert.Equal(bars[^1], run.Latest.Bar);
            if (mode == IndicatorHistoryMode.Full)
            {
                int row = 0;
                await foreach (var snapshot in run) Assert.Equal(bars[row++], snapshot.Bar);
                Assert.Equal(bars.Length, row);
            }
        }
    }
}

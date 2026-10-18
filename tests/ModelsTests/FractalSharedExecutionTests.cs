using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class FractalSharedExecutionTests
{
    [Theory]
    [InlineData(2, 0)] [InlineData(2, 1)] [InlineData(2, 513)] [InlineData(3, 513)]
    [InlineData(7, 1025)] [InlineData(20, 19)] [InlineData(20, 20)] [InlineData(20, 21)]
    [InlineData(20, 513)] [InlineData(256, 513)] [InlineData(int.MaxValue, 33)]
    public async Task RingSplitAndRoundedRecurrenceMatchOriginalState(int period, int count)
    {
        var random = new Random(4731);
        var bars = Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), i,
            i + 1, i - 1, i % 89 < 31 ? 3 : random.NextDouble() * 23 - 10, i + 2)).ToArray();
        await Check(bars, period);
    }

    [Theory]
    [InlineData(2)] [InlineData(3)] [InlineData(7)] [InlineData(20)]
    public async Task EpsilonSubnormalsAndWideConvexIntermediatesRemainExact(int period)
    {
        foreach (int scale in new[] { -1074, -1022, -500, 0, 900 })
            await Check(Enumerable.Range(0, 129).Select(i => new Bar(default, 0, 0, 0,
                Math.ScaleB((i * 13 % 23) - 11, scale), 0)).ToArray(), period);
        double[] extremes = [double.MaxValue, -double.MaxValue, double.MaxValue / 2,
            -double.MaxValue / 2, double.Epsilon, -double.Epsilon, 0d, -0d];
        await Check(Enumerable.Range(0, 129).Select(i => new Bar(default, 0, 0, 0,
            extremes[i % extremes.Length], 0)).ToArray(), period);
    }

    [Fact]
    public async Task EpsilonCertificateGridAndIntegerBoundariesMatchExactState()
    {
        foreach (int grid in new[] { -801, -800, -799, 0, 900 })
        foreach (long bound in new[] { (1L << 47) - 1, (1L << 47) + 1 })
        {
            var bars = Enumerable.Range(0, 65).Select(i => new Bar(default, 0, 0, 0,
                i % 2 == 0 ? 0 : Math.ScaleB(bound, grid), 0)).ToArray();
            await Check(bars, 4);
        }
        // Zero split with nonzero whole requires the epsilon term itself.
        await Check(new[] { 0d, 0, 1, 1, 0, 0, 1, 1 }.Select(v => new Bar(default, 0, 0, 0, v, 0)).ToArray(), 4);
    }

    [Fact]
    public async Task ConstantWindowsAndMixedDuplicateRootsPreserveIndependentState()
    {
        foreach (double price in new[] { 0d, -0d, double.Epsilon, double.MaxValue, -double.MaxValue })
            await Check(Enumerable.Repeat(new Bar(default, 0, 0, 0, price, 0), 65).ToArray(), 7);
        var bars = Enumerable.Range(0, 65).Select(i => new Bar(default, 0, 0, 0, i % 11, 0)).ToArray();
        var first = new RangeFractalAverage(3); var second = new RangeFractalAverage(7);
        var engine = new CustomIndicatorEngine(bars, _ => null);
        var a = engine.Compute(first)[0]; var b = engine.Compute(second)[0];
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(first, second, first).BuildAsync();
        Assert.Equal(a.Select(BitConverter.DoubleToInt64Bits), run[first.Value].ToArray().Select(BitConverter.DoubleToInt64Bits));
        Assert.Equal(b.Select(BitConverter.DoubleToInt64Bits), run[second.Value].ToArray().Select(BitConverter.DoubleToInt64Bits));
    }

    [Fact]
    public async Task ValidationCancellationRecoveryAndBackendContractsRemainIntact()
    {
        var bars = Enumerable.Repeat(new Bar(default, 0, 0, 0, 1, 0), 513).ToArray();
        var indicator = new RangeFractalAverage(20);
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

    private static async Task Check(Bar[] bars, int period)
    {
        var indicator = new RangeFractalAverage(period);
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

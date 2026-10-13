using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class EntropySharedExecutionTests
{
    [Theory]
    [InlineData(2, 0)] [InlineData(2, 1)] [InlineData(2, 513)] [InlineData(20, 19)]
    [InlineData(20, 20)] [InlineData(20, 21)] [InlineData(20, 1025)]
    [InlineData(4096, 8193)] [InlineData(4097, 8194)] [InlineData(int.MaxValue, 33)]
    public async Task CachedCountArithmeticPreservesOriginalBitPatterns(int period, int count)
    {
        var random = new Random(8149);
        var bars = Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), i,
            i + 1, i - 1, i % 137 < 41 ? 3 : random.Next(23), i + 2)).ToArray();
        await Check(bars, period);
    }

    [Fact]
    public async Task FrequencySlotReuseAndWideExactGroupsKeepSummationOrder()
    {
        double[] values = [0d, -0d, double.Epsilon, -double.Epsilon, double.MaxValue,
            Math.BitDecrement(double.MaxValue), -double.MaxValue, 1, Math.BitIncrement(1), 2, 3];
        var random = new Random(9154);
        var bars = Enumerable.Range(0, 2049).Select(i => new Bar(default, 0, 0, 0,
            values[i % 97 < 40 ? 3 : random.Next(values.Length)], 0)).ToArray();
        foreach (int period in new[] { 2, 3, 7, 20, 127 }) await Check(bars, period);
    }

    [Fact]
    public async Task NearConstantAndAllDistinctWindowsPreserveStartupTerms()
    {
        foreach (bool distinct in new[] { false, true })
            await Check(Enumerable.Range(0, 1025).Select(i => new Bar(default, 0, 0, 0,
                distinct ? i : i % 500 == 499 ? 2 : 1, 0)).ToArray(), 500);
    }

    [Fact]
    public async Task MixedAndDuplicateRootsRemainIndependent()
    {
        var bars = Enumerable.Range(0, 129).Select(i => new Bar(default, 0, 0, 0, i * 17 % 11, 0)).ToArray();
        var first = new NormalizedWindowEntropy(7); var second = new NormalizedWindowEntropy(20);
        var engine = new CustomIndicatorEngine(bars, _ => null);
        var a = engine.Compute(first)[0]; var b = engine.Compute(second)[0];
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(first, second, first).BuildAsync();
        Assert.Equal(a.Select(BitConverter.DoubleToInt64Bits), run[first.Value].ToArray().Select(BitConverter.DoubleToInt64Bits));
        Assert.Equal(b.Select(BitConverter.DoubleToInt64Bits), run[second.Value].ToArray().Select(BitConverter.DoubleToInt64Bits));
    }

    [Fact]
    public async Task InvalidFieldsCancellationRecoveryAndBackendContractsRemainIntact()
    {
        var bars = Enumerable.Repeat(new Bar(default, 0, 0, 0, 1, 0), 513).ToArray();
        var indicator = new NormalizedWindowEntropy(20);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator);
        bars[^1] = new(default, 0, 0, 0, 1, double.NaN);
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
        var indicator = new NormalizedWindowEntropy(period);
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

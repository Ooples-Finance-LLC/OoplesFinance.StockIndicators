using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class DeviationRatioSharedExecutionTests(Xunit.Abstractions.ITestOutputHelper log)
{
    private static Bar[] BarsOf(IEnumerable<double> prices) => prices.Select((p, i) =>
        new Bar(DateTime.UnixEpoch.AddMinutes(i), i, i + 1, i - 1, p, i + 2)).ToArray();

    [Theory]
    [InlineData(1, 4, .2)] [InlineData(3, 12, .2)] [InlineData(5, 3, .5)]
    [InlineData(3, 12, 0)] [InlineData(3, 12, -.1)] [InlineData(3, 12, 1.2)]
    [InlineData(int.MaxValue, int.MaxValue, .2)]
    public async Task StartupExpirationUndefinedAndOwnedColumnsMatchOriginal(int shortPeriod, int longPeriod, double alpha)
    {
        foreach (int count in new[] { 0, 1, 3, 12, 13, 65, 257 })
        {
            await Check(BarsOf(Enumerable.Range(0, count).Select(i => 100 + (i * 13 % 101) / 8d)),
                new(shortPeriod, longPeriod, alpha));
            await Check(BarsOf(Enumerable.Range(0, count).Select(i => i < 40 ? 7d : i % 13d)),
                new(shortPeriod, longPeriod, alpha));
        }
    }

    [Fact]
    public async Task ExtremeSubnormalAndExtendedZeroGainMatchOriginal()
    {
        foreach (int exponent in new[] { -1074, -1022, -500, 0, 900 })
            await Check(BarsOf(Enumerable.Range(0, 33).Select(i => Math.ScaleB(i % 13 - 6d, exponent))), new(2, 5, .2));
        await Check(BarsOf([double.MaxValue, -double.MaxValue, double.Epsilon, 2 * double.Epsilon, 3 * double.Epsilon]), new(5, 2, 0));
        foreach (double price in new[] { -0d, double.Epsilon, double.MaxValue, -double.MaxValue })
            await Check(BarsOf(Enumerable.Repeat(price, 17)), new(2, 5, .2));
    }

    [Fact]
    public void ResetRandomExponentSpansAndSignedGainsMatchOriginal()
    {
        var random = new Random(85113);
        var bars = BarsOf(Enumerable.Range(0, 129).Select(i => Math.ScaleB(random.NextDouble() * 2 - 1, random.Next(-1074, 1024))));
        foreach (double alpha in new[] { 0, .2, -.1, 1.2, double.Epsilon, double.MaxValue, -double.MaxValue })
        foreach (var periods in new[] { (3, 12), (5, 2), (1, 1) })
        {
            var indicator = new DeviationRatioAdaptiveAverage(periods.Item1, periods.Item2, alpha);
            var original = (IMultiOutputState)indicator.CreateState();
            var compact = new ValuesBarExecution.DeviationRatioValueState(indicator, bars.Length);
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
    public void OrdinaryWindowsReducePaddedArithmeticAllocations()
    {
        var bars = BarsOf(Enumerable.Range(0, 256).Select(i => 100 + (i * 13 % 101) / 8d));
        var indicator = new DeviationRatioAdaptiveAverage(20);
        var original = (IMultiOutputState)indicator.CreateState();
        var compact = new ValuesBarExecution.DeviationRatioValueState(indicator, bars.Length);
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
        Assert.True(newBytes < oldBytes / 4, $"Compact {newBytes}, original {oldBytes}");
    }

    [Fact]
    public async Task ValidationCancellationAndBackendContractsRecover()
    {
        var bars = BarsOf(Enumerable.Repeat(1d, 33));
        var indicator = new DeviationRatioAdaptiveAverage(3);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator);
        bars[^1] = new(default, 1, 1, 1, 1, double.NaN);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
        bars[^1] = new(default, 1, 1, 1, 1, 1);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        using var run = await builder.ConfigureExecution(IndicatorExecutionBackend.Auto).BuildAsync();
        Assert.Equal(IndicatorExecutionBackend.Cpu, builder.LastExecution!.Backend);
        Assert.Equal(0, run[indicator.IsDefined].ToArray()[^1]);
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
    }

    [Fact]
    public async Task MixedDuplicateRootsAndOverflowValidationOrderArePreserved()
    {
        var bars = BarsOf(Enumerable.Range(0, 65).Select(i => 100 + i % 7d));
        var first = new DeviationRatioAdaptiveAverage(3, 12);
        var second = new DeviationRatioAdaptiveAverage(7, 3, -.1);
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
        var overflow = BarsOf([1d, 2d, 10d, 20d]);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(overflow))
            .ConfigureIndicators(new DeviationRatioAdaptiveAverage(2, 2, double.MaxValue));
        overflow[^1] = new(default, 1, 1, 1, 20, double.NaN);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
        overflow[^1] = new(default, 1, 1, 1, 20, 1);
        await Assert.ThrowsAsync<OoplesFinance.StockIndicators.Validation.IndicatorOutputException>(() => builder.BuildAsync());
    }

    private static async Task Check(Bar[] bars, DeviationRatioAdaptiveAverage indicator)
    {
        var state = (IMultiOutputState)indicator.CreateState();
        var expected = Enumerable.Range(0, 2).Select(_ => new double[bars.Length]).ToArray();
        var scratch = new double[2];
        for (int i = 0; i < bars.Length; i++)
        {
            state.Update(bars[i], scratch);
            for (int slot = 0; slot < 2; slot++) expected[slot][i] = scratch[slot];
        }
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var input = bars.ToArray();
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input))
                .ConfigureIndicators(indicator).ConfigureHistory(mode).BuildAsync();
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

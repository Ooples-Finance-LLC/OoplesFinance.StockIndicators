using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class HilbertTrendlineSharedExecutionTests(Xunit.Abstractions.ITestOutputHelper log)
{
    private static Bar[] BarsOf(IEnumerable<double> prices) => prices.Select((p, i) =>
        new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p + 2, p - 1, p, i + 2)).ToArray();
    private static IIndicator Create(int kind) => kind == 0 ? new OldestFirstHilbertTrendline() : new SeededHilbertTrendline(kind == 1);
    private static object State(IIndicator indicator) => indicator is IndicatorBase scalar ? scalar.CreateState()! : ((MultiOutputIndicatorBase)indicator).CreateState()!;
    private static void Update(object state, in Bar bar, double[] output)
    {
        if (state is IIndicatorState scalar) output[0] = scalar.Update(bar);
        else ((IMultiOutputState)state).Update(bar, output);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task StartupRingExpirationAndEveryOwnedColumnMatchOriginal(int kind)
    {
        foreach (int count in new[] { 0, 1, 5, 6, 7, 10, 11, 12, 51, 129, 513 })
            await Check(BarsOf(Enumerable.Range(0, count).Select(i => i % 97 < 22 ? 100 : 100 + (i * 13 % 101) / 8d)), Create(kind));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task WideMidpointsSubnormalRoundingAndSignedZeroRemainExact(int kind)
    {
        foreach (double scale in new[] { double.MaxValue, double.Epsilon, 1e-200, -0d })
        foreach (bool alternate in new[] { false, true })
        {
            var bars = Enumerable.Range(0, 129).Select(i =>
            {
                double price = alternate && i % 2 == 1 ? -scale : scale;
                return new Bar(DateTime.UnixEpoch.AddMinutes(i), price, price, price, price, 0);
            }).ToArray();
            await Check(bars, Create(kind));
        }
        var random = new Random(3351);
        await Check(BarsOf(Enumerable.Range(0, 129).Select(i => Math.ScaleB(random.NextDouble() * 2 - 1, random.Next(-1074, 1024)))), Create(kind));
    }

    [Fact]
    public void OldestFirstStateResetAndBothRingsMatchOriginal()
    {
        var original = (IIndicatorState)new OldestFirstHilbertTrendline().CreateState();
        var compact = new ValuesBarExecution.OldestHilbertValueState();
        var bars = BarsOf(Enumerable.Range(0, 513).Select(i => (i * 13 % 101 - 50) / 8d));
        for (int replay = 0; replay < 2; replay++)
        {
            original.Reset(); compact.Reset();
            foreach (var bar in bars) Assert.Equal(BitConverter.DoubleToInt64Bits(original.Update(bar)), BitConverter.DoubleToInt64Bits(compact.Update(bar)));
        }
    }

    [Fact]
    public void OldestFirstRingsRemovePerUpdateIteratorAllocations()
    {
        var bars = BarsOf(Enumerable.Range(0, 256).Select(i => 100 + (i * 13 % 101) / 8d));
        var original = (IIndicatorState)new OldestFirstHilbertTrendline().CreateState();
        var compact = new ValuesBarExecution.OldestHilbertValueState();
        long Measure(IIndicatorState state)
        {
            state.Reset();
            long before = GC.GetAllocatedBytesForCurrentThread();
            foreach (var bar in bars) state.Update(bar);
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
        Measure(original); Measure(compact);
        long oldBytes = Measure(original), newBytes = Measure(compact);
        log.WriteLine($"256 updates: original {oldBytes} bytes, compact {newBytes} bytes.");
        Assert.True(oldBytes > 0); Assert.Equal(0, newBytes);
    }

    [Fact]
    public async Task MixedDuplicateRootsRetainIndependentState()
    {
        var bars = BarsOf(Enumerable.Range(0, 129).Select(i => 100 + i % 7d));
        var first = new OldestFirstHilbertTrendline(); var second = new SeededHilbertTrendline();
        var engine = new CustomIndicatorEngine(bars, _ => null);
        var a = engine.Compute(first); var b = engine.Compute(second);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(first, second, first).ConfigureHistory(history).BuildAsync();
            AssertBits(a[0], run[first.Outputs[0]].ToArray());
            for (int slot = 0; slot < 6; slot++) AssertBits(b[slot], run[second.Outputs[slot]].ToArray());
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public async Task ValidationCancellationAndBackendContractsRecover(int kind)
    {
        var bars = BarsOf(Enumerable.Repeat(1d, 33)); var indicator = Create(kind);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator);
        bars[^1] = new(default, 1, 1, 1, 1, double.NaN);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
        bars[^1] = new(default, 1, 3, 0, 1, 1);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        using var run = await builder.ConfigureExecution(IndicatorExecutionBackend.Auto).BuildAsync();
        Assert.Equal(IndicatorExecutionBackend.Cpu, builder.LastExecution!.Backend);
        Assert.Equal(kind == 0 ? 1 : 1.5, run[indicator.Outputs[0]].ToArray()[^1]);
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
    }

    private static async Task Check(Bar[] bars, IIndicator indicator)
    {
        object state = State(indicator); int columns = indicator.Outputs.Count;
        var expected = Enumerable.Range(0, columns).Select(_ => new double[bars.Length]).ToArray();
        var scratch = new double[columns];
        for (int i = 0; i < bars.Length; i++)
        {
            Update(state, bars[i], scratch);
            for (int slot = 0; slot < columns; slot++) expected[slot][i] = scratch[slot];
        }
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var input = bars.ToArray();
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input))
                .ConfigureIndicators(indicator).ConfigureHistory(mode).BuildAsync();
            Array.Clear(input); run.Dispose();
            for (int slot = 0; slot < columns; slot++) AssertBits(expected[slot], run[indicator.Outputs[slot]].ToArray());
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

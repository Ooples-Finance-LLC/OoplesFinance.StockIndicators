using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit.Abstractions;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class ContainmentSharedExecutionTests(ITestOutputHelper log)
{
    private static IndicatorBase Create(int kind, int period) => kind switch
    {
        0 => new HaramiCandle(period), 1 => new HaramiCrossCandle(period),
        2 => new HomingPigeonCandle(period), _ => new DojiStarCandle(period)
    };
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task ExactDecisionsAndOwnedHistoryMatchOriginalAndIndependentReference(int kind)
    {
        foreach (int period in new[] { 1, 3, 10, int.MaxValue - 1 })
        foreach (int count in new[] { 0, 1, 9, 10, 11, 12, 65 })
        {
            var bars = Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i),
                i % 3 == 0 ? 9.5 : .5, 10, 0, i % 7 == 0 ? 4 : i % 3 == 0 ? 9.5 : .5, i + 1)).ToArray();
            await Check(bars, Create(kind, period));
        }
        var random = new Random(990 + kind);
        foreach (double scale in new[] { double.Epsilon, double.MaxValue / 32, 1e-200, -0d })
            await Check(Enumerable.Range(0, 65).Select(i => new Bar(default, (i % 5 - 2) * scale,
                (i % 7 + 2) * scale, (i % 3 - 4) * scale, (i % 5 - 2) * scale, 1)).ToArray(), Create(kind, 3));
        double Next() => Math.ScaleB(random.NextDouble() * 2 - 1, random.Next(-1074, 1020));
        var varied = Enumerable.Range(0, 129).Select(_ => new Bar(default, Next(), Next(), Next(), Next(), 1)).ToArray();
        await Check(varied, Create(kind, 7));
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void ExistingOrdinaryStateUpdatesAllocateNothing(int kind)
    {
        var owner = Create(kind, 10);
        var original = (IIndicatorState)owner.CreateState();
        var bar = new Bar(default, 5, 10, 0, 5, 1);
        for (int i = 0; i < 256; i++) original.Update(bar);
        long Measure(IIndicatorState state)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 256; i++) state.Update(bar);
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
        long bytes = Measure(original);
        log.WriteLine($"kind={kind}; existing256={bytes}; construction excluded");
        Assert.Equal(0, bytes);
    }
    [Fact]
    public async Task MixedRootsValidationAndCancellationRecover()
    {
        var bars = Enumerable.Repeat(new Bar(default, 5, 10, 0, 5, 1), 65).ToArray();
        var roots = Enumerable.Range(0, 4).Select(k => Create(k, 10)).ToArray();
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots).ConfigureHistory(mode);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
            bars[^1] = new(default, 5, 10, 0, 5, double.NaN);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
            bars[^1] = new(default, 5, 10, 0, 5, 1);
            using var run = await builder.ConfigureExecution(IndicatorExecutionBackend.Auto).BuildAsync();
            Assert.Equal(IndicatorExecutionBackend.Cpu, builder.LastExecution!.Backend);
            foreach (var root in roots)
            foreach (var rule in ((IIndicatorValidationContract)root).ValidationRules)
                rule.Check(new IndicatorValidationContext("Mixed containment", bars, [run[root].ToArray()], 0));
        }
    }
    private static async Task Check(Bar[] bars, IndicatorBase owner)
    {
        var state = (IIndicatorState)owner.CreateState();
        var expected = bars.Select(b => state.Update(b)).ToArray();
        Assert.True(ValuesBarExecution.SupportsOwned([owner]));
        foreach (var rule in ((IIndicatorValidationContract)owner).ValidationRules)
            rule.Check(new IndicatorValidationContext("Containment reference", bars, [expected], 0));
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var input = bars.ToArray();
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input)).ConfigureIndicators(owner).ConfigureHistory(mode).BuildAsync();
            Array.Clear(input); run.Dispose(); Assert.Equal(expected, run[owner].ToArray());
            if (bars.Length > 0) Assert.Equal(bars[^1], run.Latest.Bar);
            if (mode == IndicatorHistoryMode.Full)
            {
                int row = 0; await foreach (var snapshot in run) Assert.Equal(bars[row++], snapshot.Bar);
                Assert.Equal(bars.Length, row);
            }
        }
    }
}

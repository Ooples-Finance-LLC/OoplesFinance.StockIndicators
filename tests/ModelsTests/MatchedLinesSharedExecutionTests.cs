using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit.Abstractions;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class MatchedLinesSharedExecutionTests(ITestOutputHelper log)
{
    private static IndicatorBase Create(int kind, int period, int equalPeriod = 5, int shadowPeriod = 10) => kind switch
    {
        0 => new CounterattackCandle(period, equalPeriod),
        1 => new SeparatingLinesCandle(period, shadowPeriod, equalPeriod),
        _ => new GapSideBySideWhiteLinesCandle(period, equalPeriod)
    };
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task ExactDecisionsAndOwnedHistoryMatchOriginalAndIndependentReference(int kind)
    {
        int max = int.MaxValue - (kind == 2 ? 2 : 1);
        foreach (int period in new[] { 1, 3, 10, max })
        foreach (int equalPeriod in new[] { 1, 5, max })
        foreach (int count in new[] { 0, 1, 9, 10, 11, 12, 65 })
        {
            var bars = Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i),
                i % 3 == 0 ? 9.5 : .5, 10, 0, i % 7 == 0 ? 4 : i % 3 == 0 ? 9.5 : .5, i + 1)).ToArray();
            await Check(bars, Create(kind, period, equalPeriod));
        }
        if (kind == 1)
            await Check([new Bar(default, 1, 2, 0, 1, 1)], Create(kind, 1, 1, int.MaxValue - 1));
        var random = new Random(990 + kind);
        foreach (double scale in new[] { double.Epsilon, double.MaxValue / 32, 1e-200, -0d })
            await Check(Enumerable.Range(0, 65).Select(i => new Bar(default, (i % 5 - 2) * scale,
                (i % 7 + 2) * scale, (i % 3 - 4) * scale, (i % 5 - 2) * scale, 1)).ToArray(), Create(kind, 3));
        double Next() => Math.ScaleB(random.NextDouble() * 2 - 1, random.Next(-1074, 1020));
        var varied = Enumerable.Range(0, 129).Select(_ => new Bar(default, Next(), Next(), Next(), Next(), 1)).ToArray();
        await Check(varied, Create(kind, 7));
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
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
        var roots = Enumerable.Range(0, 3).Select(k => Create(k, 10)).ToArray();
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
                rule.Check(new IndicatorValidationContext("Mixed matched-lines", bars, [run[root].ToArray()], 0));
        }
    }
    [Theory]
    [InlineData(false, double.NaN)]
    [InlineData(false, double.NegativeInfinity)]
    [InlineData(true, double.NaN)]
    [InlineData(true, double.NegativeInfinity)]
    public void GapSideNonfiniteOpenRetainsEqualityErrorBeforeStateChanges(bool previous, double invalid)
    {
        var owner = new GapSideBySideWhiteLinesCandle(1, 1);
        var state = (IIndicatorState)owner.CreateState();
        var seed = new Bar(default, 5, 20, -10, 6, 1);
        var bars = new List<Bar> { seed, seed, previous ? new(default, invalid, 20, -10, 6, 1) : seed };
        // GapSide windows accumulate ranges only, so a nonfinite previous
        // open can legitimately survive warmup in direct-state execution.
        foreach (var bar in bars) Assert.Equal(0d, state.Update(bar));
        var attempt = previous ? seed : new Bar(default, invalid, 20, -10, 6, 1);
        for (int repeat = 0; repeat < 2; repeat++)
        {
            var error = Assert.Throws<ArithmeticException>(() => state.Update(attempt));
            Assert.Equal("An exact finite mean requires finite inputs.", error.Message);
        }
        // The failed attempt must not enter either range queue or shift the
        // retained candles. Continue with both independent positive signals.
        Bar[] continuation = [
            new(default, 6, 20, -10, 5, 1),
            seed, new(default, 8, 20, -10, 9, 1), new(default, 8, 20, -10, 9, 1),
            new(default, 8, 20, -10, 9, 1), seed, seed
        ];
        var values = new List<double> { 0, 0, 0 };
        foreach (var bar in continuation) { bars.Add(bar); values.Add(state.Update(bar)); }
        Assert.Contains(100d, values); Assert.Contains(-100d, values);
        foreach (var rule in owner.ValidationRules)
            rule.Check(new IndicatorValidationContext("GapSide equality error continuation", bars.ToArray(), [values.ToArray()], 0));
    }

    [Fact]
    public async Task GapSideStrictGapBoundariesKeepBothSignalDirections()
    {
        var bars = Enumerable.Repeat(new Bar(default, 5, 20, -10, 6, 1), 3).ToList();
        void Triple(double anchorOpen, double anchorClose, double open, double close)
        {
            bars.Add(new(default, anchorOpen, 20, -10, anchorClose, 1));
            bars.Add(new(default, open, 20, -10, close, 1));
            bars.Add(new(default, open, 20, -10, close, 1));
        }
        Triple(5, 6, 6, 7); // Touching the upper body edge is not a gap.
        Triple(5, 6, Math.BitIncrement(6d), 7);
        Triple(8, 9, 6, 8); // Touching the lower body edge is not a gap.
        Triple(8, 9, 6, Math.BitDecrement(8d));
        Triple(-0d, +0d, +0d, 1);
        var owner = new GapSideBySideWhiteLinesCandle(1, 1);
        var state = (IIndicatorState)owner.CreateState();
        var values = bars.Select(bar => state.Update(bar)).ToArray();
        Assert.Equal(0d, values[5]); Assert.Equal(100d, values[8]);
        Assert.Equal(0d, values[11]); Assert.Equal(-100d, values[14]);
        Assert.Equal(0d, values[17]);
        await Check(bars.ToArray(), owner);
    }

    private static async Task Check(Bar[] bars, IndicatorBase owner)
    {
        var state = (IIndicatorState)owner.CreateState();
        var expected = bars.Select(b => state.Update(b)).ToArray();
        Assert.True(ValuesBarExecution.SupportsOwned([owner]));
        foreach (var rule in ((IIndicatorValidationContract)owner).ValidationRules)
            rule.Check(new IndicatorValidationContext("Matched-lines reference", bars, [expected], 0));
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

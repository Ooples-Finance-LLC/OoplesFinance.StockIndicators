using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit.Abstractions;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class AlligatorSharedExecutionTests(ITestOutputHelper log)
{
    private static Bar[] Data(int count) => Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 100, 110 + i % 17, 90 - i % 13, 100 + i % 7, 1)).ToArray();
    private static MultiOutputIndicatorBase Create(bool gator, int[] s) => gator
        ? new GatorWithDetails(s[0], s[1], s[2], s[3], s[4], s[5])
        : new AlligatorWithDetails(s[0], s[1], s[2], s[3], s[4], s[5]);
    public static IEnumerable<object[]> Settings => new[]
    {
        new[] { 13, 8, 8, 5, 5, 3 }, new[] { 3, 1, 2, 1, 1, 1 },
        new[] { 7, 7, 5, 5, 3, 3 }, new[] { 3, int.MaxValue, 2, int.MaxValue, 1, int.MaxValue },
        new[] { int.MaxValue, 3, int.MaxValue - 1, 2, int.MaxValue - 2, 1 }
    }.Select(s => new object[] { s });

    [Theory, MemberData(nameof(Settings))]
    public void CompactDelayRingsMatchOriginalAcrossResetAndExtremeInputs(int[] settings)
    {
        var owner = (AlligatorWithDetails)Create(false, settings);
        var random = new Random(1739);
        var randomBars = Enumerable.Range(0, 129).Select(_ => { double v = Math.ScaleB(random.Next(-100, 101), random.Next(-1074, 1017)); return new Bar(default, v, v, v, v, 1); }).ToArray();
        var extreme = Enumerable.Range(0, 129).Select(i => { double v = i % 2 == 0 ? double.MaxValue : -double.MaxValue; return new Bar(default, v, v, v, v, 1); }).ToArray();
        foreach (bool gator in new[] { false, true })
        foreach (int count in new[] { 0, 1, 7, 20, 21, 129 })
        {
            var original = (IMultiOutputState)Create(gator, settings).CreateState();
            IMultiOutputState compact = new ValuesBarExecution.AlligatorState(owner, count);
            if (gator) compact = new GatorWithDetails.State(compact);
            foreach (var source in new[] { Data(129), randomBars, extreme, Enumerable.Repeat(new Bar(default, 0, -0d, 0d, 0, 1), 129).ToArray(), Data(129) })
            {
                original.Reset(); compact.Reset();
                var expected = new double[gator ? 8 : 6]; var actual = new double[expected.Length];
                foreach (var bar in source.Take(count))
                {
                    Array.Fill(expected, 17); Array.Fill(actual, 29);
                    original.Update(in bar, expected); compact.Update(in bar, actual);
                    Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
                }
            }
        }
    }

    [Fact]
    public void UnpublishedDelaysAvoidGrowingStorage()
    {
        var owner = new AlligatorWithDetails(3, int.MaxValue, 2, int.MaxValue, 1, int.MaxValue);
        var bars = Data(4000); var output = new double[6];
        long Measure(bool shared)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            IMultiOutputState state = shared ? new ValuesBarExecution.AlligatorState(owner, bars.Length) : (IMultiOutputState)owner.CreateState();
            foreach (var bar in bars) state.Update(in bar, output);
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
        Measure(false); Measure(true);
        long original = Measure(false), compact = Measure(true);
        log.WriteLine($"4000 all-warmup updates including construction: original={original} bytes, compact={compact} bytes.");
        Assert.True(compact < original / 2, $"Original {original}; compact {compact}.");
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AllColumnsPreserveWarmupExpansionTiesAndOwnership(bool gator)
    {
        foreach (int count in new[] { 0, 1, 4, 7, 12, 19, 20, 21, 65, 513, 1001 }) await Check(Data(count), Create(gator, [13, 8, 8, 5, 5, 3]));
        foreach (var row in Settings) await Check(Data(65), Create(gator, (int[])row[0]));
        await Check(Enumerable.Repeat(new Bar(default, 1, 1, 1, 1, 1), 65).ToArray(), Create(gator, [3, 1, 2, 1, 1, 1]));
        foreach (double scale in new[] { double.Epsilon, 1e-200, -1d, Math.ScaleB(1d, 900) })
            await Check(Data(65).Select(b => new Bar(b.Time, b.Open * scale, Math.Max(b.High * scale, b.Low * scale), Math.Min(b.High * scale, b.Low * scale), b.Close * scale, 1)).ToArray(), Create(gator, [7, 7, 5, 5, 3, 3]));
    }

    [Fact]
    public async Task GatorOverflowKeepsLaterInputPriority()
    {
        var bars = Enumerable.Range(0, 513).Select(i => { double v = i < 3 ? double.MaxValue : -double.MaxValue; return new Bar(default, v, v, v, v, 1); }).ToArray();
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var owner = new GatorWithDetails(3, 4, 2, 2, 1, 1);
            var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).ConfigureHistory(mode);
            var ordinary = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).ConfigureHistory(mode).ConfigureBehavior(_ => { });
            var expected = await Assert.ThrowsAsync<IndicatorOutputException>(() => ordinary.BuildAsync());
            var actual = await Assert.ThrowsAsync<IndicatorOutputException>(() => builder.BuildAsync());
            Assert.Equal(expected.Message, actual.Message);
            var valid = bars[500]; bars[500] = new(valid.Time, valid.Open, valid.High, valid.Low, valid.Close, double.NaN);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync()); bars[500] = valid;
        }
    }

    [Fact]
    public async Task MixedChainedRootsRetainCancellationAndBackendContracts()
    {
        var bars = Data(513); var roots = new[] { Create(false, [3, 1, 2, 1, 1, 1]), Create(true, [7, 7, 5, 5, 3, 3]) };
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots.Concat(new[] { roots[0] }).ToArray()).ConfigureHistory(mode);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
            using var run = await builder.ConfigureExecution(IndicatorExecutionBackend.Auto).BuildAsync();
            Assert.Equal(IndicatorExecutionBackend.Cpu, builder.LastExecution!.Backend);
            foreach (var owner in roots)
            {
                using var ordinary = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).ConfigureBehavior(_ => { }).BuildAsync();
                Columns(run, owner, owner.Outputs.Select(o => ordinary[o].ToArray()).ToArray());
            }
            await Assert.ThrowsAnyAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
        }
        foreach (var root in roots) { root.Of(new FirstValueEma(5)); await Check(bars, root, false); }
    }

    private static async Task Check(Bar[] bars, IIndicator owner, bool reference = true)
    {
        using var ordinary = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).ConfigureBehavior(_ => { }).BuildAsync();
        var expected = owner.Outputs.Select(o => ordinary[o].ToArray()).ToArray();
        if (reference)
            foreach (var rule in ((IIndicatorValidationContract)owner).ValidationRules)
                rule.Check(new IndicatorValidationContext("Alligator shared execution", bars, expected, 0));
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var input = bars.ToArray();
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input)).ConfigureIndicators(owner).ConfigureHistory(mode).BuildAsync();
            Array.Clear(input); run.Dispose(); Columns(run, owner, expected);
            if (bars.Length > 0) Assert.Equal(bars[^1], run.Latest.Bar);
            if (mode == IndicatorHistoryMode.Full)
            {
                int row = 0; await foreach (var snapshot in run) Assert.Equal(bars[row++], snapshot.Bar);
                Assert.Equal(bars.Length, row);
            }
        }
    }
    private static void Columns(IIndicatorRun run, IIndicator owner, double[][] expected)
    {
        for (int slot = 0; slot < expected.Length; slot++)
            Assert.Equal(expected[slot].Select(BitConverter.DoubleToInt64Bits), run[owner.Outputs[slot]].ToArray().Select(BitConverter.DoubleToInt64Bits));
    }
}

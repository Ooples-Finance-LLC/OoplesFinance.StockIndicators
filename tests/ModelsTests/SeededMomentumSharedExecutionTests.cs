using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit.Abstractions;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class SeededMomentumSharedExecutionTests(ITestOutputHelper log)
{
    private static Bar[] BarsOf(IEnumerable<double> prices) => prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
    private static Bar[] Data(int count) => BarsOf(Enumerable.Range(0, count).Select(i => 100 + (i * 17 % 31) / 8d));
    private static MultiOutputIndicatorBase Create(int kind, int period = 3, int signal = 3) => kind switch
    {
        0 => new SeededPriceMomentum(Math.Max(2, period), 3, signal),
        1 => new SeededTrueStrength(period, 3, signal),
        _ => new TripleExponentialRate(period, (TripleRateSeed)(kind - 2), signal)
    };

    [Theory]
    [InlineData(TripleRateSeed.SharedMean)]
    [InlineData(TripleRateSeed.CascadedMeans)]
    [InlineData(TripleRateSeed.CascadedFirstPrice)]
    public void CompactTripleStagesMatchOriginalBitsAcrossResetAndWideInputs(TripleRateSeed seed)
    {
        var ordinary = Data(97).Select(b => b.Close).ToArray();
        var random = new Random(739);
        var exponents = Enumerable.Range(0, 97).Select(_ => Math.ScaleB(random.Next(-100, 101), random.Next(-1074, 1017))).ToArray();
        double[] extremes = [double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon, -0d, 0d, 1, -1, Math.BitIncrement(1d)];
        foreach (int period in new[] { 1, 2, 7, int.MaxValue })
        foreach (int? signal in new int?[] { null, 1, 5, int.MaxValue })
        foreach (int suppression in seed == TripleRateSeed.SharedMean ? new[] { 0 } : new[] { 0, 2, int.MaxValue })
        {
            var owner = new TripleExponentialRate(period, seed, signal, suppression);
            var original = (IMultiOutputState)owner.CreateState();
            var compact = new ValuesBarExecution.TripleRateState(period, seed, signal, suppression);
            foreach (var prices in new[] { ordinary, extremes.Concat(ordinary).ToArray(), exponents,
                Enumerable.Repeat(-1d, 97).ToArray(), Enumerable.Repeat(-0d, 97).ToArray(), ordinary })
            {
                original.Reset(); compact.Reset();
                var expected = new double[6]; var actual = new double[6];
                foreach (var bar in BarsOf(prices))
                {
                    Array.Fill(expected, 17); Array.Fill(actual, 29);
                    original.Update(in bar, expected); compact.Update(in bar, actual);
                    Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
                }
            }
        }
    }

    [Fact]
    public void CompactTripleUpdatesReduceOrdinaryAllocation()
    {
        var bars = Data(97); var output = new double[6];
        var original = (IMultiOutputState)new TripleExponentialRate(20).CreateState();
        var compact = new ValuesBarExecution.TripleRateState(20, TripleRateSeed.SharedMean, null, 0);
        long Measure(IMultiOutputState state)
        {
            for (int i = 0; i < 1000; i++) state.Update(in bars[i % bars.Length], output);
            state.Reset();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 4000; i++) state.Update(in bars[i % bars.Length], output);
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
        long baseline = Measure(original), changed = Measure(compact);
        log.WriteLine($"4000 warmed updates including reset/startup: original={baseline} bytes, compact={changed} bytes.");
        Assert.True(changed < baseline / 2, $"Original {baseline} bytes; compact {changed} bytes.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task SeededOutputsRetainWarmupMissingValuesAndOwnedColumns(int kind)
    {
        foreach (int count in new[] { 0, 1, 2, 3, 7, 19, 65, 513, 1001 }) await Check(Data(count), Create(kind));
        foreach (int signal in new[] { 1, 3, int.MaxValue }) await Check(Data(65), Create(kind, 3, signal));
        await Check(Data(65), Create(kind, int.MaxValue));
        if (kind == 1) await Check(Data(65), new SeededTrueStrength(3, 1, 0));
        foreach (double flat in new[] { -1d, 0d, -0d }) await Check(BarsOf(Enumerable.Repeat(flat, 65)), Create(kind));
        await Check(BarsOf(new[] { 1d, 0, 2, 3 }.Concat(Data(65).Select(b => b.Close))), Create(kind));
        foreach (double scale in new[] { double.Epsilon, 1e-200, Math.ScaleB(1d, 900) })
            await Check(BarsOf(Data(65).Select(b => b.Close * scale)), Create(kind));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublishedOverflowKeepsLaterRawInputPriority(bool triple)
    {
        var bars = BarsOf(new[] { double.Epsilon, double.MaxValue }.Concat(Enumerable.Repeat(1d, 511)));
        IIndicator owner = triple ? new TripleExponentialRate(1, signalPeriod: 1) : new SeededPriceMomentum(2, 1, 1);
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
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
        var bars = Data(513); var roots = Enumerable.Range(0, 5).Select(k => Create(k)).ToArray();
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
                rule.Check(new IndicatorValidationContext("Seeded momentum shared execution", bars, expected, 0));
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

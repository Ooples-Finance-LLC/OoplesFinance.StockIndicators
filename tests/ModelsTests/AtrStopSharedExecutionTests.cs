using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Xunit.Abstractions;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class AtrStopSharedExecutionTests(ITestOutputHelper log)
{
    private static Bar[] Data(int count) => Enumerable.Range(0, count).Select(i =>
        new Bar(DateTime.UnixEpoch.AddMinutes(i), 100, 110 + i % 17, 90 - i % 13, 100 + i % 7, 1)).ToArray();
    private static MultiOutputIndicatorBase Create(int kind, int period = 3) => kind < 3
        ? new WindowChandelierExit(period, 3, (ChandelierExitSelection)kind)
        : new SeededAtrTrailingStop(Math.Max(2, period), 3, (AtrTrailBasis)(kind - 3));

    [Theory]
    [InlineData(ChandelierExitSelection.Long)]
    [InlineData(ChandelierExitSelection.Short)]
    [InlineData(ChandelierExitSelection.Both)]
    public void DequesMatchOriginalAcrossWrapResetTiesAndExtremePrices(ChandelierExitSelection selection)
    {
        var ordinary = Data(129);
        var extremes = Enumerable.Range(0, 65).Select(i => new Bar(default, 0, double.MaxValue, -double.MaxValue, i % 2 == 0 ? double.MaxValue : -double.MaxValue, 1)).ToArray();
        var flat = Enumerable.Range(0, 129).Select(i => new Bar(default, 0, i % 2 == 0 ? -0d : 0d, i % 2 == 0 ? 0d : -0d, 0, 1)).ToArray();
        foreach (int period in new[] { 1, 3, 20, int.MaxValue })
        foreach (double multiplier in new[] { 0d, -0d, -3d, 3d, double.Epsilon, double.MaxValue })
        foreach (bool floor in new[] { false, true })
        {
            var owner = new WindowChandelierExit(period, multiplier, selection, floor);
            var original = (IMultiOutputState)owner.CreateState();
            var compact = new ValuesBarExecution.ChandelierState(owner, 129);
            foreach (var bars in new[] { ordinary, extremes, flat, ordinary })
            {
                original.Reset(); compact.Reset();
                var expected = new double[4]; var actual = new double[4];
                foreach (var bar in bars)
                {
                    Array.Fill(expected, 17); Array.Fill(actual, 29);
                    original.Update(in bar, expected); compact.Update(in bar, actual);
                    Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
                }
            }
        }
    }

    [Fact]
    public void DequesRemovePerBarExtremaScanAllocations()
    {
        var bars = Data(97); var output = new double[4]; var owner = new WindowChandelierExit(20);
        long Measure(IMultiOutputState state)
        {
            for (int i = 0; i < 1000; i++) state.Update(in bars[i % bars.Length], output);
            state.Reset(); long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 4000; i++) state.Update(in bars[i % bars.Length], output);
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
        long original = Measure((IMultiOutputState)owner.CreateState());
        long compact = Measure(new ValuesBarExecution.ChandelierState(owner, bars.Length));
        log.WriteLine($"4000 warmed updates including reset/startup: original={original} bytes, deque={compact} bytes.");
        Assert.True(compact < original, $"Original {original}; deque {compact}.");
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public async Task EverySelectionAndBasisRetainsOwnedOutputs(int kind)
    {
        foreach (int count in new[] { 0, 1, 2, 3, 20, 65, 513, 1001 }) await Check(Data(count), Create(kind));
        await Check(Data(65), Create(kind, int.MaxValue));
        foreach (double scale in new[] { double.Epsilon, 1e-200, -1d, Math.ScaleB(1d, 900) })
            await Check(Data(65).Select(b => new Bar(b.Time, b.Open * scale, Math.Max(b.High * scale, b.Low * scale), Math.Min(b.High * scale, b.Low * scale), b.Close * scale, b.Volume)).ToArray(), Create(kind));
        if (kind < 3)
            await Check(Data(65).Select(b => new Bar(b.Time, -b.Open, -b.Low, -b.High, -b.Close, 1)).ToArray(), new WindowChandelierExit(3, -3, (ChandelierExitSelection)kind, true));
    }

    [Fact]
    public async Task OnlySelectedLevelOverflowsAndRawInputStillHasPriority()
    {
        var bars = new[] { new Bar(default, 0, 0, 0, 0, 1), new Bar(default, 0, double.MaxValue, 0, 1, 1) };
        await Check(bars, new WindowChandelierExit(1, 2, ChandelierExitSelection.Long));
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var owner = new WindowChandelierExit(1, 2);
            var input = bars.Concat(Data(511)).ToArray();
            var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(input)).ConfigureIndicators(owner).ConfigureHistory(mode);
            var ordinary = new StockIndicatorBuilder().ConfigureSource(Bars.From(input)).ConfigureIndicators(owner).ConfigureHistory(mode).ConfigureBehavior(_ => { });
            var expected = await Assert.ThrowsAsync<IndicatorOutputException>(() => ordinary.BuildAsync());
            var actual = await Assert.ThrowsAsync<IndicatorOutputException>(() => builder.BuildAsync());
            Assert.Equal(expected.Message, actual.Message);
            var valid = input[500]; input[500] = new(valid.Time, valid.Open, valid.High, valid.Low, valid.Close, double.NaN);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
        }
    }

    [Fact]
    public async Task MixedChainedRootsRetainCancellationAndBackendContracts()
    {
        var bars = Data(513); var roots = Enumerable.Range(0, 6).Select(k => Create(k)).ToArray();
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
                rule.Check(new IndicatorValidationContext("ATR stop shared execution", bars, expected, 0));
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

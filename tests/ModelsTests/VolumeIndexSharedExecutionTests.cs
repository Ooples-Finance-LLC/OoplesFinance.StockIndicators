using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class VolumeIndexSharedExecutionTests
{
    private static Bar[] Data(int count) => Enumerable.Range(0, count).Select(i =>
        new Bar(DateTime.UnixEpoch.AddMinutes(i), 32, 128, -128, i % 2 == 0 ? -32 - i % 7 : 32 + i % 7, i % 5)).ToArray();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SignedRatiosHoldsStartupAndOwnedColumnsStayExact(bool positive)
    {
        foreach (int count in new[] { 0, 1, 2, 31, 513, 1001 })
            await Check(Data(count), new VolumeConditionedIndex(positive));
        foreach (double scale in new[] { double.Epsilon, 1e-200, Math.ScaleB(1d, 900) })
            await Check(Data(65).Select(b => new Bar(b.Time, b.Open * scale, b.High * scale, b.Low * scale, b.Close * scale, b.Volume)).ToArray(), new VolumeConditionedIndex(positive));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ZeroDenominatorClearsDirtyScratchAndResetRestoresPresence(bool positive)
    {
        var owner = new VolumeConditionedIndex(positive);
        var state = (VolumeConditionedIndex.State)owner.CreateState();
        var output = new[] { 123d, 456d };
        foreach (double zero in new[] { 0d, -0d })
        {
            state.Reset();
            state.Update(new Bar(default, zero, zero, zero, zero, 2), output);
            Assert.Equal(new[] { 100d, 1 }, output);
            state.Update(new Bar(default, 1, 1, 1, 1, positive ? 3 : 1), output);
            Assert.Equal(new[] { 0d, 0 }, output);
            Array.Fill(output, 789);
            state.Update(new Bar(default, 2, 2, 2, 2, 2), output);
            Assert.Equal(new[] { 0d, 0 }, output);
            var bars = Data(513);
            bars[0] = new(default, zero, zero, zero, zero, 2);
            bars[1] = new(default, 1, 1, 1, 1, positive ? 3 : 1);
            await Check(bars, owner);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OverflowKeepsRawInputPriority(bool positive)
    {
        var bars = Data(513);
        bars[0] = new(default, double.Epsilon, double.Epsilon, double.Epsilon, double.Epsilon, 2);
        bars[1] = new(default, double.MaxValue, double.MaxValue, double.MaxValue, double.MaxValue, positive ? 3 : 1);
        // Hold after overflow: a second selected update would feed infinity back
        // into exact arithmetic before either execution path validates output.
        for (int i = 2; i < bars.Length; i++)
        {
            var bar = bars[i];
            bars[i] = new(bar.Time, bar.Open, bar.High, bar.Low, bar.Close, bars[1].Volume);
        }
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var owner = new VolumeConditionedIndex(positive);
            var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).ConfigureHistory(mode);
            var ordinary = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).ConfigureHistory(mode).ConfigureBehavior(_ => { });
            var expected = await Assert.ThrowsAsync<IndicatorOutputException>(() => ordinary.BuildAsync());
            var actual = await Assert.ThrowsAsync<IndicatorOutputException>(() => builder.BuildAsync());
            Assert.Equal(expected.Message, actual.Message);
            var valid = bars[500];
            bars[500] = new(valid.Time, valid.Open, valid.High, valid.Low, valid.Close, double.NaN);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
            bars[500] = valid;
        }
    }

    [Fact]
    public async Task MixedDuplicateAndChainedRootsRetainBackendAndCancellationContracts()
    {
        var bars = Data(513);
        var positive = new VolumeConditionedIndex(true);
        var negative = new VolumeConditionedIndex();
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(positive, negative, positive).ConfigureHistory(mode);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
            using var run = await builder.ConfigureExecution(IndicatorExecutionBackend.Auto).BuildAsync();
            Assert.Equal(IndicatorExecutionBackend.Cpu, builder.LastExecution!.Backend);
            foreach (var owner in new[] { positive, negative })
            {
                using var ordinary = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).ConfigureBehavior(_ => { }).BuildAsync();
                Columns(run, owner, owner.Outputs.Select(o => ordinary[o].ToArray()).ToArray());
            }
            await Assert.ThrowsAnyAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
        }
        positive.Of(new FirstValueEma(5));
        await Check(bars, positive, false);
    }

    private static async Task Check(Bar[] bars, VolumeConditionedIndex owner, bool reference = true)
    {
        using var ordinary = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).ConfigureBehavior(_ => { }).BuildAsync();
        var expected = owner.Outputs.Select(o => ordinary[o].ToArray()).ToArray();
        if (reference)
            foreach (var rule in owner.ValidationRules)
                rule.Check(new IndicatorValidationContext("Volume index shared execution", bars, expected, 0));
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var input = bars.ToArray();
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input)).ConfigureIndicators(owner).ConfigureHistory(mode).BuildAsync();
            Array.Clear(input);
            run.Dispose();
            Columns(run, owner, expected);
            if (bars.Length > 0) Assert.Equal(bars[^1], run.Latest.Bar);
            if (mode == IndicatorHistoryMode.Full)
            {
                int row = 0;
                await foreach (var snapshot in run) Assert.Equal(bars[row++], snapshot.Bar);
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

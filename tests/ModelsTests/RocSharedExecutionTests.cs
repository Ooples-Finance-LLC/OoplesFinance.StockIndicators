using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class RocSharedExecutionTests
{
    private static Bar[] Data(int count) => Enumerable.Range(0, count).Select(i =>
        new Bar(DateTime.UnixEpoch.AddMinutes(i), 32, 128, -128, i % 2 == 0 ? -32 - i % 7 : 32 + i % 7, i % 5)).ToArray();

    [Theory]
    [InlineData(1, null)]
    [InlineData(1, 1)]
    [InlineData(3, 5)]
    [InlineData(20, 20)]
    [InlineData(63, 1)]
    public async Task FiniteReplayRetainsWarmupPartitionEdgesAndAllColumns(int lag, int? average)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int dop in new[] { 1, 8 })
            {
                CpuParallelSettings.MaxDegreeOfParallelism = dop;
                foreach (int count in new[] { 0, 1, lag, lag + (average ?? 1), 511, 513, 1001, 10000 })
                    await Check(Data(count), new RateOfChangeWithAverage(lag, average), count < 1002);
                var bars = Data(1001);
                foreach (int i in new[] { 0, 124, 125, 249, 500, 875 }) bars[i] = new(bars[i].Time, 0, 0, -0d, -0d, 1);
                await Check(bars, new RateOfChangeWithAverage(lag, average));
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Fact]
    public async Task MaximumPeriodsFallBackWithoutOverflowingLookback()
    {
        await Check(Data(513), new RateOfChangeWithAverage(int.MaxValue, int.MaxValue));
        await Check(Data(513), new RateOfChangeWithAverage(1, int.MaxValue));
        await Check(Data(513), new RateOfChangeRmsBands(int.MaxValue, int.MaxValue, int.MaxValue));
        await Check(Data(513), new RateOfChangeRmsBands(1, int.MaxValue, 1));
        await Check(Data(1001), new RateOfChangeWithAverage(4096, 1));
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(3, 5, 2)]
    [InlineData(20, 3, 3)]
    public async Task BandsStaySequentialAndRecoverWithoutRestartingMissingEma(int lag, int ema, int deviation)
    {
        var owner = new RateOfChangeRmsBands(lag, ema, deviation);
        foreach (int count in new[] { 0, 1, lag, lag + ema, 513, 1001 }) await Check(Data(count), owner);
        var bars = Data(1001);
        bars[0] = new(default, 0, 0, 0, 0, 1);
        await Check(bars, owner);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).BuildAsync();
        Assert.All(run[owner.EmaIsDefined].ToArray(), v => Assert.Equal(0d, v));
        Assert.Equal(1d, run[owner.BandsAreDefined].ToArray()[^1]);
        var flat = Enumerable.Repeat(new Bar(default, 1, 1, 1, 1, 1), 65).ToArray();
        await Check(flat, owner);
        using var flatRun = await new StockIndicatorBuilder().ConfigureSource(Bars.From(flat)).ConfigureIndicators(owner).BuildAsync();
        Assert.Equal(BitConverter.DoubleToInt64Bits(-0d), BitConverter.DoubleToInt64Bits(flatRun[owner.LowerBand].ToArray()[^1]));
    }

    [Fact]
    public async Task ScaledPricesAndHugeIntermediateProductsRemainExact()
    {
        foreach (double scale in new[] { double.Epsilon, 1e-200, Math.ScaleB(1d, 900) })
        {
            var bars = Data(65).Select(b => new Bar(b.Time, b.Open * scale, b.High * scale, b.Low * scale, b.Close * scale, b.Volume)).ToArray();
            await Check(bars, new RateOfChangeWithAverage(3, 5));
            await Check(bars, new RateOfChangeRmsBands(3, 5, 3));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SecondaryMomentumOverflowAndRocOverflowKeepInputPriority(bool bands)
    {
        foreach (bool momentumOnly in bands ? new[] { false } : new[] { false, true })
        {
            var bars = Data(513);
            double first = momentumOnly ? -double.MaxValue : double.Epsilon;
            bars[0] = new(default, first, first, first, first, 1);
            bars[1] = new(default, double.MaxValue, double.MaxValue, double.MaxValue, double.MaxValue, 1);
            IIndicator owner = bands ? new RateOfChangeRmsBands(1, 1, 1) : new RateOfChangeWithAverage(1, 1);
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
            {
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
    }

    [Fact]
    public async Task MixedChainedRootsPreserveCancellationAndBackendContracts()
    {
        var bars = Data(513);
        var average = new RateOfChangeWithAverage(3);
        var bands = new RateOfChangeRmsBands(3);
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(average, bands, average).ConfigureHistory(mode);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
            using var run = await builder.ConfigureExecution(IndicatorExecutionBackend.Auto).BuildAsync();
            Assert.Equal(IndicatorExecutionBackend.Cpu, builder.LastExecution!.Backend);
            foreach (var owner in new IIndicator[] { average, bands })
            {
                using var ordinary = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).ConfigureBehavior(_ => { }).BuildAsync();
                Columns(run, owner, owner.Outputs.Select(o => ordinary[o].ToArray()).ToArray());
            }
            await Assert.ThrowsAnyAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
        }
        average.Of(new FirstValueEma(5));
        bands.Of(new FirstValueEma(5));
        await Check(bars, average, false);
        await Check(bars, bands, false);
    }

    private static async Task Check(Bar[] bars, IIndicator owner, bool reference = true)
    {
        using var ordinary = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).ConfigureBehavior(_ => { }).BuildAsync();
        var expected = owner.Outputs.Select(o => ordinary[o].ToArray()).ToArray();
        if (reference)
            foreach (var rule in ((IIndicatorValidationContract)owner).ValidationRules)
                rule.Check(new IndicatorValidationContext("ROC shared execution", bars, expected, 0));
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

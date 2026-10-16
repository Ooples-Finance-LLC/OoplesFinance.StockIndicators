using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class PriceVolumeDetailsSharedExecutionTests
{
    private static Bar[] Data(int count) => Enumerable.Range(0, count).Select(i =>
        new Bar(DateTime.UnixEpoch.AddMinutes(i), 100, 110 + i % 17, 90 - i % 13, 100 + i % 7, i % 13)).ToArray();
    private static MultiOutputIndicatorBase Create(int kind, int period = 3) => kind switch
    {
        0 => new BalanceOfPowerWithValidity(period), 1 => new ElderRayWithDetails(period), _ => new PriceAdjustedAccumulationDistribution()
    };

    [Theory]
    [InlineData(1)] [InlineData(3)] [InlineData(20)] [InlineData(63)]
    public async Task BopReplayPreservesMissingWindowRecoveryAtPartitionEdges(int period)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int dop in new[] { 1, 8 })
            {
                CpuParallelSettings.MaxDegreeOfParallelism = dop;
                await Check(Data(10000), new BalanceOfPowerWithValidity(period), false);
                var bars = Data(1001);
                foreach (int i in new[] { 0, 124, 125, 249, 500, 875 }) bars[i] = new(bars[i].Time, 0, 0, -0d, -0d, 1);
                await Check(bars, new BalanceOfPowerWithValidity(period));
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task OwnedOutputsRetainStartupExtremeScaleAndHugePeriods(int kind)
    {
        foreach (int count in new[] { 0, 1, 2, 3, 20, 65, 513, 1001 }) await Check(Data(count), Create(kind));
        await Check(Data(65), Create(kind, int.MaxValue));
        if (kind == 0) await Check(Data(513), Create(kind, 4097));
        foreach (double scale in new[] { double.Epsilon, 1e-200, -1d, Math.ScaleB(1d, 900) })
            await Check(Data(65).Select(b => new Bar(b.Time, b.Open * scale, Math.Max(b.High * scale, b.Low * scale), Math.Min(b.High * scale, b.Low * scale), b.Close * scale, b.Volume)).ToArray(), Create(kind));
    }

    [Fact]
    public async Task AdjustedAdlFlatZeroDenominatorStaysAbsent()
    {
        var bars = Data(513); bars[0] = new(default, 0, 0, 0, 0, 7); bars[1] = new(default, 1, 1, 1, 1, 5);
        var owner = new PriceAdjustedAccumulationDistribution(); await Check(bars, owner);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).BuildAsync();
        Assert.Equal(7d, run[owner.Value].ToArray()[0]);
        Assert.All(run[owner.IsDefined].ToArray().Skip(1), v => Assert.Equal(0d, v));
    }

    [Fact]
    public async Task WorkerArithmeticExceptionsRemainUnwrappedAndRawInputWins()
    {
        var bars = Data(1001);
        bars[510] = new(default, -double.MaxValue, double.Epsilon, 0, double.MaxValue, 1);
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
            {
                var owner = new BalanceOfPowerWithValidity(3);
                var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).ConfigureHistory(mode);
                var ordinary = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).ConfigureHistory(mode).ConfigureBehavior(_ => { });
                var expected = await Assert.ThrowsAsync<ArithmeticException>(() => ordinary.BuildAsync());
                var actual = await Assert.ThrowsAsync<ArithmeticException>(() => builder.BuildAsync());
                Assert.Equal(expected.Message, actual.Message);
                var valid = bars[900]; bars[900] = new(valid.Time, valid.Open, valid.High, valid.Low, valid.Close, double.NaN);
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync()); bars[900] = valid;
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SecondaryPowerAndAccumulationOverflowKeepInputPriority(bool adl)
    {
        var bars = Data(513);
        if (adl) { bars[0] = new(default, 0, 2, 0, 2, double.MaxValue); bars[1] = bars[0]; }
        else bars[0] = new(default, 0, double.MaxValue, -double.MaxValue, -double.MaxValue, 1);
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            IIndicator owner = adl ? new PriceAdjustedAccumulationDistribution() : new ElderRayWithDetails(1);
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
    public async Task MixedChainedRootsPreserveCancellationAndBackendContracts()
    {
        var bars = Data(513); var roots = Enumerable.Range(0, 3).Select(k => Create(k)).ToArray();
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
                rule.Check(new IndicatorValidationContext("Price/volume details shared execution", bars, expected, 0));
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

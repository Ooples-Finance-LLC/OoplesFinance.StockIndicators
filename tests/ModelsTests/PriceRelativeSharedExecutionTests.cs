using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class PriceRelativeSharedExecutionTests
{
    private static Bar[] Data(int count) => Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), i % 31 == 0 ? -0d : 100 + i % 11, 110 + i % 17, 90 - i % 13, i % 17 == 0 ? 0 : 100 + i % 7, i % 13)).ToArray();
    [Theory]
    [InlineData(null, null)] [InlineData(null, 1)] [InlineData(1, null)] [InlineData(1, 1)]
    [InlineData(20, 3)] [InlineData(3, 20)] [InlineData(4095, 1)] [InlineData(4096, 1)]
    [InlineData(1, 4096)] [InlineData(1, 4097)] [InlineData(int.MaxValue, int.MaxValue)]
    public async Task ReplayMissingRecoveryAndLargePeriodFallbackStayExact(int? period, int? mean)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int dop in new[] { 1, 8 })
            {
                CpuParallelSettings.MaxDegreeOfParallelism = dop;
                foreach (int count in new[] { 0, 1, 65, 511, 513, 10000 })
                    await Check(Data(count), new PriceRelativeStrength(period, mean), count <= 65);
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Fact]
    public async Task AllFieldPairsAndSignedInputsRetainPresenceColumns()
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            var bars = Data(513).Select((b, i) => i % 3 == 0 ? new Bar(b.Time, -b.Open, -b.High, -b.Low, -b.Close, b.Volume) : b).ToArray();
            foreach (var evaluation in Enum.GetValues<CandlePriceField>())
                foreach (var basis in Enum.GetValues<CandlePriceField>())
                    await Check(bars, new PriceRelativeStrength(7, 3, evaluation, basis));
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Fact]
    public async Task HugeReturnTermsCancelExactlyAcrossWorkers()
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int dop in new[] { 1, 8 })
            {
                CpuParallelSettings.MaxDegreeOfParallelism = dop;
                var bars = Enumerable.Range(0, 1001).Select(i => {
                    double price = i % 4 == 0 ? double.Epsilon : i % 4 == 1 ? double.MaxValue : i % 4 == 2 ? -double.Epsilon : -double.MaxValue;
                    return new Bar(default, price, double.MaxValue, -double.MaxValue, price, -0d);
                }).ToArray();
                await Check(bars, new PriceRelativeStrength(1, 3));
                await Check(bars, new PriceRelativeStrength(20, 1));
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RatioAndReturnOverflowRetainInputAndSlotPriority(bool ratio)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int dop in new[] { 1, 8 })
            {
                CpuParallelSettings.MaxDegreeOfParallelism = dop;
                var bars = Data(1001);
                bars[509] = new(default, double.Epsilon, 1, 0, 2 * double.Epsilon, 1);
                bars[510] = new(default, ratio ? double.Epsilon : double.MaxValue, 1, 0, double.MaxValue, 1);
                var owner = new PriceRelativeStrength(1, 1);
                foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
                {
                    var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).ConfigureHistory(mode);
                    var ordinary = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).ConfigureHistory(mode).ConfigureBehavior(_ => { });
                    var expected = await Assert.ThrowsAsync<IndicatorOutputException>(() => ordinary.BuildAsync());
                    var actual = await Assert.ThrowsAsync<IndicatorOutputException>(() => builder.BuildAsync());
                    Assert.Equal(expected.Message, actual.Message);
                    var valid = bars[900]; bars[900] = new(valid.Time, valid.Open, valid.High, valid.Low, valid.Close, double.NaN);
                    await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync()); bars[900] = valid;
                    if (!ratio)
                    {
                        bars[900] = new(default, double.Epsilon, 1, 0, double.MaxValue, 1);
                        expected = await Assert.ThrowsAsync<IndicatorOutputException>(() => ordinary.BuildAsync());
                        actual = await Assert.ThrowsAsync<IndicatorOutputException>(() => builder.BuildAsync());
                        Assert.Equal(expected.Message, actual.Message);
                        bars[900] = valid;
                    }
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Fact]
    public async Task MixedChainedRootsPreserveCancellationAndBackendContracts()
    {
        var bars = Data(513); MultiOutputIndicatorBase[] roots = [new PriceRelativeStrength(), new PriceRelativeStrength(20, 3), new RangeAccelerationBands(20)];
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
        var chained = new PriceRelativeStrength(20, 3); chained.Of(new FirstValueEma(5)); await Check(bars, chained, false);
    }

    private static async Task Check(Bar[] bars, IIndicator owner, bool reference = true)
    {
        using var ordinary = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).ConfigureBehavior(_ => { }).BuildAsync();
        var expected = owner.Outputs.Select(o => ordinary[o].ToArray()).ToArray();
        if (reference)
            foreach (var rule in ((IIndicatorValidationContract)owner).ValidationRules)
                rule.Check(new IndicatorValidationContext("Price relative shared execution", bars, expected, 0));
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
        {
            Assert.Equal(expected[slot].Select(BitConverter.DoubleToInt64Bits), run[owner.Outputs[slot]].ToArray().Select(BitConverter.DoubleToInt64Bits));
            if (expected[slot].Length > 0)
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected[slot][^1]), BitConverter.DoubleToInt64Bits(run.Latest[owner.Outputs[slot]]));
        }
    }
}

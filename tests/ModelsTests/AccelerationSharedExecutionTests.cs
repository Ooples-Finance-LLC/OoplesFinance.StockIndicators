using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class AccelerationSharedExecutionTests
{
    [Fact]
    public void CompactTransformedInputsMatchIndependentRationalBits()
    {
        var random = new Random(80411);
        for (int i = 0; i < 1000; i++)
        {
            int exponent = i % 361 - 180;
            double high = Math.ScaleB(80 + random.NextDouble() * 40, exponent);
            double low = Math.ScaleB(80 + random.NextDouble() * 40, exponent);
            if ((i & 1) != 0) low = -low;
            Assert.True(ValuesBarExecution.TryAccelerationInputs(high, low, out double upper, out double lower));
            var expected = RangeAccelerationReference.Values([new Bar(default, 0, high, low, 0, 0)], 1);
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected[0][0]!.Value), BitConverter.DoubleToInt64Bits(upper));
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected[2][0]!.Value), BitConverter.DoubleToInt64Bits(lower));
        }
        Assert.False(ValuesBarExecution.TryAccelerationInputs(double.Epsilon, 2 * double.Epsilon, out _, out _));
        foreach (double high in new[] { 1d, -1d, Math.ScaleB(1d, -180), Math.ScaleB(-1d, 180) })
        foreach (double low in new[] { Math.BitIncrement(-high), Math.BitDecrement(-high) })
        {
            Assert.True(ValuesBarExecution.TryAccelerationInputs(high, low, out double upper, out double lower));
            var expected = RangeAccelerationReference.Values([new Bar(default, 0, high, low, 0, 0)], 1);
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected[0][0]!.Value), BitConverter.DoubleToInt64Bits(upper));
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected[2][0]!.Value), BitConverter.DoubleToInt64Bits(lower));
        }
        Assert.False(ValuesBarExecution.TryAccelerationInputs(double.MaxValue, double.MaxValue / 2, out _, out _));
        foreach (double high in new[] { double.Epsilon, 0d, -0d, double.MaxValue })
        {
            Assert.True(ValuesBarExecution.TryAccelerationInputs(high, -high, out double upper, out double lower));
            Assert.Equal(high, upper); Assert.Equal(-high, lower);
        }
    }

    [Theory]
    [InlineData(1)] [InlineData(20)] [InlineData(129)]
    public async Task CompactRollingSumsPreserveWideFallbackAndExpiration(int period)
    {
        double[] magnitudes = [Math.ScaleB(1d, -1074), Math.ScaleB(1d, -500), 1d,
            Math.ScaleB(1d, 180), Math.ScaleB(1d, 500)];
        var bars = Enumerable.Range(0, 257).Select(i =>
        {
            double scale = magnitudes[i / 53 % magnitudes.Length];
            return new Bar(default, 0, scale * (i % 7 + 1), scale * (i % 5 - 2), scale * (i % 3), 0);
        }).ToArray();
        await Check(bars, new RangeAccelerationBands(period));
    }

    private static Bar[] Data(int count) => Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 100, 110 + i % 17, 90 - i % 13, 100 + i % 7, i % 13)).ToArray();
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(20)] [InlineData(129)]
    [InlineData(4096)] [InlineData(4097)] [InlineData(int.MaxValue)]
    public async Task ReplayStartupFallbackAndAllColumnsStayExact(int period)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int dop in new[] { 1, 8 })
            {
                CpuParallelSettings.MaxDegreeOfParallelism = dop;
                foreach (int count in new[] { 0, 1, 511, 513, 1001, 8191, 8193, 10000 })
                    await Check(Data(count), new RangeAccelerationBands(period), count < 1002);
                foreach (double scale in new[] { double.Epsilon, 1e-200, -1d, Math.ScaleB(1d, 900) })
                    await Check(Data(513).Select(b => new Bar(b.Time, b.Open * scale, b.High * scale, b.Low * scale, b.Close * scale, b.Volume)).ToArray(), new RangeAccelerationBands(period));
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Fact]
    public async Task ExtendedInputsCancelAcrossWorkerBoundaries()
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int dop in new[] { 1, 8 })
            {
                CpuParallelSettings.MaxDegreeOfParallelism = dop;
                var bars = Enumerable.Range(0, 10000).Select(i => new Bar(default, 0, i % 2 == 0 ? double.MaxValue : -double.MaxValue, 0, -0d, 1)).ToArray();
                await Check(bars, new RangeAccelerationBands(2));
                await Check(bars, new RangeAccelerationBands(20));
                var zeroSum = Enumerable.Range(0, 10000).Select(i => new Bar(default, -0d, i % 2 == 0 ? double.MaxValue : double.Epsilon, i % 2 == 0 ? -double.MaxValue : -double.Epsilon, -0d, -0d)).ToArray();
                await Check(zeroSum, new RangeAccelerationBands(20));
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task PrimaryAndSecondaryOverflowRetainRawInputPriority(bool lower)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int dop in new[] { 1, 8 })
            {
                CpuParallelSettings.MaxDegreeOfParallelism = dop;
                var bars = Data(10000);
                bars[1250] = lower ? new(default, 0, 0, -double.MaxValue, 0, 1) : new(default, 0, double.MaxValue, 0, 0, 1);
                var owner = new RangeAccelerationBands(1);
                foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
                {
                    var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).ConfigureHistory(mode);
                    var ordinary = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).ConfigureHistory(mode).ConfigureBehavior(_ => { });
                    var expected = await Assert.ThrowsAsync<IndicatorOutputException>(() => ordinary.BuildAsync());
                    var actual = await Assert.ThrowsAsync<IndicatorOutputException>(() => builder.BuildAsync());
                    Assert.Equal(expected.Message, actual.Message);
                    var valid = bars[9000]; bars[9000] = new(valid.Time, valid.Open, valid.High, valid.Low, valid.Close, double.NaN);
                    await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync()); bars[9000] = valid;
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Fact]
    public async Task MixedChainedRootsPreserveCancellationAndBackendContracts()
    {
        var bars = Data(1001); MultiOutputIndicatorBase[] roots = [new RangeAccelerationBands(1), new RangeAccelerationBands(20), new PriceCandlePattern()];
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
        var chained = new RangeAccelerationBands(20); chained.Of(new FirstValueEma(5)); await Check(bars, chained, false);
    }

    private static async Task Check(Bar[] bars, IIndicator owner, bool reference = true)
    {
        using var ordinary = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).ConfigureBehavior(_ => { }).BuildAsync();
        var expected = owner.Outputs.Select(o => ordinary[o].ToArray()).ToArray();
        if (reference)
            foreach (var rule in ((IIndicatorValidationContract)owner).ValidationRules)
                rule.Check(new IndicatorValidationContext("Acceleration shared execution", bars, expected, 0));
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

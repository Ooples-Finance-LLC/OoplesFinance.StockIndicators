using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class TensorsCmfGpuExecutionTests
{
    [SkippableTheory]
    [InlineData(1)] [InlineData(2)] [InlineData(17)]
    [InlineData(127)] [InlineData(128)] [InlineData(129)] [InlineData(int.MaxValue)]
    public async Task DeviceFlowsAndWindowsMatchExactReference(int period)
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        foreach (int count in new[] { 1, 127, 128, 129, 513 })
            await Check(Data(count), new MoneyFlowWithDetails(period));
    }

    [SkippableFact]
    public async Task NormalizedPricesSignedVolumesAndZeroPreserveEveryOutputBit()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        foreach (int priceScale in new[] { -900, 900 })
        foreach (int volumeScale in new[] { -400, 400 })
            await Check(Data(257, priceScale, volumeScale), new MoneyFlowWithDetails(17));
        var subnormal = Enumerable.Range(0, 129).Select(i =>
        {
            double low = (i % 7) * double.Epsilon, range = (1 << (i % 3)) * double.Epsilon;
            return new Bar(default, 0, low + range, low, low, i % 11 - 5);
        }).ToArray();
        await Check(subnormal, new MoneyFlowWithDetails(2));
        await Check([new(default, -0d, -0d, 0, -0d, -0d), new(default, 0, 1, -1, -0d, -1)],
            new MoneyFlowWithDetails(1));
    }

    [SkippableFact]
    public async Task FourColumnWorkspaceSwitchesPreserveRetainedResults()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var bars = Enumerable.Range(0, 257).Select(i =>
        {
            double close = 20 + (i % 11) / 8d;
            return new Bar(default, i, close + 1.5, close - .5, close, i % 13 + 1);
        }).ToArray();
        var cmf = new MoneyFlowWithDetails(14);
        using var expected = await Build(bars, cmf).ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
        using var retained = await Build(bars, cmf).BuildAsync();
        foreach (IIndicator indicator in new IIndicator[]
        {
            new SeededOnBalanceVolume(averagePeriod: 14), cmf, new AccumulationDistributionWithAverage(14), cmf, new SeededForceIndex(13), cmf, new WindowVortex(14), cmf,
            new WindowUltimateOscillator(), cmf, new WindowMoneyFlowIndex(14, minimumTotalOne: true), cmf,
            new Sma(14), cmf, new TrueRange(1), cmf, new BullishCandle(), cmf
        }) await Check(bars, indicator);
        retained.Dispose();
        Equal(cmf, expected, retained);
    }

    [SkippableFact]
    public async Task RejectionInputPrecedenceCancellationAndRecoveryRemainExplicit()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var bars = Data(257);
        var original = bars.ToArray();
        var cmf = new MoneyFlowWithDetails(257);
        var builder = Build(bars, cmf);
        using var saved = await builder.BuildAsync();
        bars[0] = new(default, 0, 3, 0, 1, 1);
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync());
        Assert.Null(builder.LastExecution);
        bars[^1] = new(default, double.NaN, 1, 0, 1, 1);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
        Assert.Null(builder.LastExecution);
        foreach (double volume in new[] { double.MaxValue, double.Epsilon, (1L << 40) + 1d })
        {
            for (int i = 0; i < bars.Length; i++) bars[i] = new(default, 0, 1, 0, 1, i == 0 ? 1 : volume);
            await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync());
            Assert.Null(builder.LastExecution);
        }
        for (int i = 0; i < bars.Length; i++)
            bars[i] = new(default, 0, (1 << 20) + 1, 1 << 20, (1 << 20) + 1, (1L << 36) + 1d);
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync());
        Assert.Null(builder.LastExecution);
        Array.Copy(original, bars, bars.Length);
        using var recovered = await builder.BuildAsync();
        Equal(cmf, saved, recovered);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
        await Assert.ThrowsAsync<NotSupportedException>(() => Build([], cmf).BuildAsync());
        var automaticBuilder = Build(bars, cmf).ConfigureExecution(IndicatorExecutionBackend.Auto);
        using var automatic = await automaticBuilder.BuildAsync();
        Assert.Equal(IndicatorExecutionBackend.Cpu, automaticBuilder.LastExecution!.Backend);
        Equal(cmf, saved, automatic);
    }

    [SkippableFact]
    public async Task ZeroMassCancellationAndNearPowerOfTwoRatiosRemainExact()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        foreach (int period in new[] { 1, 2, 3, 20 })
        {
            // Exact integer totals give ratios above/below powers of two and
            // cancellation to zero, including negative total volume.
            var bars = Enumerable.Range(0, 513).Select(i =>
            {
                double volume = (i % 9) switch { 0 => 0, 1 => 1, 2 => -1, 3 => 255, 4 => -255,
                    5 => 256, 6 => -256, 7 => 257, _ => -257 };
                return new Bar(default, 0, 8, 0, i % 17 - 4, volume);
            }).ToArray();
            await Check(bars, new MoneyFlowWithDetails(period));
        }
        var zeros = Enumerable.Range(0, 129).Select(i =>
            new Bar(default, 0, 1, 0, .5, i % 2 == 0 ? 1 : -1)).ToArray();
        await Check(zeros, new MoneyFlowWithDetails(2));
        await Check(zeros, new MoneyFlowWithDetails(1));
        var indicator = new MoneyFlowWithDetails(2);
        using var absent = await Build(zeros, indicator).BuildAsync();
        Assert.All(absent[indicator.CmfIsDefined].ToArray(), value => Assert.Equal(0, value));
    }

    [SkippableFact]
    public async Task RandomSignedIntegerWindowsAndLargeRangeSpreadsMatchExactArithmetic()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var random = new Random(42751);
        var bars = Enumerable.Range(0, 1031).Select(i =>
        {
            double low = random.Next(-512, 513), range = 1 << random.Next(0, 9);
            double high = low + range, close = random.Next(-512, 513);
            if (i % 3 == 0) (high, low) = (low, high);
            return new Bar(default, 0, high, low, close, random.Next(-1024, 1025));
        }).ToArray();
        foreach (int period in new[] { 1, 3, 17, 127 }) await Check(bars, new MoneyFlowWithDetails(period));
        // Different dyadic ranges use one exact flow grid on device.
        await Check([new(default, 0, 1, 0, 1, 1), new(default, 0, 1 << 18, 0, 1, -1),
            new(default, 0, 2, 0, 1, 3)], new MoneyFlowWithDetails(2));
    }

    [Fact]
    public void OnlyStandaloneUnsourcedCmfIsEligible()
    {
        var cmf = new MoneyFlowWithDetails(14);
        Assert.True(ValuesBarExecution.SupportsGpu([cmf]));
        Assert.False(ValuesBarExecution.SupportsGpu([cmf, new Sma(14)]));
        Assert.False(ValuesBarExecution.SupportsGpu([cmf.Of(new Sma(14))]));
    }

    private static Bar[] Data(int count, int priceScale = 0, int volumeScale = 0) =>
        Enumerable.Range(0, count).Select(i =>
        {
            double low = (i * 13 % 101) / 8d - 6;
            double range = i % 31 == 0 ? 0 : 1 << (i % 3);
            double high = low + range, close = low + range * (i % 13 - 2) / 8;
            if (i % 7 == 0) (high, low) = (low, high);
            return new Bar(DateTime.UnixEpoch.AddMinutes(i), i, Math.ScaleB(high, priceScale),
                Math.ScaleB(low, priceScale), Math.ScaleB(close, priceScale), Math.ScaleB(i % 17 - 8, volumeScale));
        }).ToArray();

    private static async Task Check(Bar[] bars, IIndicator indicator)
    {
        using var expected = await Build(bars, indicator).ConfigureExecution(IndicatorExecutionBackend.Cpu)
            .ConfigureBehavior(_ => { }).BuildAsync();
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var input = bars.ToArray();
            var builder = Build(input, indicator).ConfigureHistory(history);
            using var actual = await builder.BuildAsync();
            Assert.Equal(IndicatorExecutionBackend.Gpu, builder.LastExecution!.Backend);
            Array.Clear(input);
            actual.Dispose();
            Equal(indicator, expected, actual);
            Assert.Equal(bars[^1], actual.Latest.Bar);
            if (history == IndicatorHistoryMode.Full)
            {
                int row = 0;
                await foreach (var snapshot in actual) Assert.Equal(bars[row++], snapshot.Bar);
                Assert.Equal(bars.Length, row);
            }
        }
    }

    private static void Equal(IIndicator indicator, IIndicatorRun expected, IIndicatorRun actual)
    {
        foreach (var output in indicator.Outputs)
            Assert.Equal(expected[output].ToArray().Select(BitConverter.DoubleToInt64Bits),
                actual[output].ToArray().Select(BitConverter.DoubleToInt64Bits));
    }

    private static StockIndicatorBuilder Build(Bar[] bars, IIndicator indicator) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator)
            .ConfigureExecution(IndicatorExecutionBackend.Gpu);
}

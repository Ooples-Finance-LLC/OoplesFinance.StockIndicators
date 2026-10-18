using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class TensorsAccumulationDistributionGpuExecutionTests
{
    [SkippableTheory]
    [InlineData(null)] [InlineData(1)] [InlineData(2)] [InlineData(17)]
    [InlineData(127)] [InlineData(128)] [InlineData(129)] [InlineData(int.MaxValue)]
    public async Task DeviceFlowsPrefixesAndInclusiveAverageMatchExactReference(int? period)
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        foreach (int count in new[] { 1, 127, 128, 129, 513 })
            await Check(Data(count), new AccumulationDistributionWithAverage(period));
    }

    [SkippableFact]
    public async Task NormalizedPricesSignedVolumesAndZeroPreserveEveryOutputBit()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        foreach (int priceScale in new[] { -900, 900 })
        foreach (int volumeScale in new[] { -400, 400 })
            await Check(Data(257, priceScale, volumeScale), new AccumulationDistributionWithAverage(17));
        var subnormal = Enumerable.Range(0, 129).Select(i =>
        {
            double low = (i % 7) * double.Epsilon, range = (1 << (i % 3)) * double.Epsilon;
            return new Bar(default, 0, low + range, low, low, i % 11 - 5);
        }).ToArray();
        await Check(subnormal, new AccumulationDistributionWithAverage(2));
        await Check([new(default, -0d, -0d, 0, -0d, -0d), new(default, 0, 1, -1, -0d, -1)],
            new AccumulationDistributionWithAverage(1));
    }

    [SkippableFact]
    public async Task FiveColumnWorkspaceSwitchesPreserveRetainedResults()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var bars = Enumerable.Range(0, 257).Select(i =>
        {
            double close = 20 + (i % 11) / 8d;
            return new Bar(default, i, close + 1.5, close - .5, close, i % 13 + 1);
        }).ToArray();
        var adl = new AccumulationDistributionWithAverage(14);
        using var expected = await Build(bars, adl).ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
        using var retained = await Build(bars, adl).BuildAsync();
        foreach (IIndicator indicator in new IIndicator[]
        {
            new SeededOnBalanceVolume(averagePeriod: 14), adl, new WindowVortex(14), adl,
            new WindowUltimateOscillator(), adl, new WindowMoneyFlowIndex(14, minimumTotalOne: true), adl,
            new Sma(14), adl, new TrueRange(1), adl, new BullishCandle(), adl
        }) await Check(bars, indicator);
        retained.Dispose();
        Equal(adl, expected, retained);
    }

    [SkippableFact]
    public async Task RejectionInputPrecedenceCancellationAndRecoveryRemainExplicit()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var bars = Data(257);
        var original = bars.ToArray();
        var adl = new AccumulationDistributionWithAverage(257);
        var builder = Build(bars, adl);
        using var saved = await builder.BuildAsync();
        bars[0] = new(default, 0, 3, 0, 1, 1);
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync());
        Assert.Null(builder.LastExecution);
        bars[^1] = new(default, double.NaN, 1, 0, 1, 1);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
        Assert.Null(builder.LastExecution);
        foreach (double volume in new[] { double.MaxValue, double.Epsilon, (1L << 38) + 1d })
        {
            for (int i = 0; i < bars.Length; i++) bars[i] = new(default, 0, 1, 0, 1, i == 0 ? 1 : volume);
            await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync());
            Assert.Null(builder.LastExecution);
        }
        Array.Copy(original, bars, bars.Length);
        using var recovered = await builder.BuildAsync();
        Equal(adl, saved, recovered);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
        await Assert.ThrowsAsync<NotSupportedException>(() => Build([], adl).BuildAsync());
        var automaticBuilder = Build(bars, adl).ConfigureExecution(IndicatorExecutionBackend.Auto);
        using var automatic = await automaticBuilder.BuildAsync();
        Assert.Equal(IndicatorExecutionBackend.Cpu, automaticBuilder.LastExecution!.Backend);
        Equal(adl, saved, automatic);
    }

    [Fact]
    public void OnlyStandaloneUnsourcedAdlIsEligible()
    {
        var adl = new AccumulationDistributionWithAverage(14);
        Assert.True(ValuesBarExecution.SupportsGpu([adl]));
        Assert.False(ValuesBarExecution.SupportsGpu([adl, new Sma(14)]));
        Assert.False(ValuesBarExecution.SupportsGpu([adl.Of(new Sma(14))]));
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

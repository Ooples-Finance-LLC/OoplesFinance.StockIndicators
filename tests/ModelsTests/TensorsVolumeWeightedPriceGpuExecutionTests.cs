using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class TensorsVolumeWeightedPriceGpuExecutionTests
{
    [SkippableTheory]
    [InlineData(null)] [InlineData(1)] [InlineData(2)] [InlineData(17)]
    [InlineData(127)] [InlineData(128)] [InlineData(129)] [InlineData(int.MaxValue)]
    public async Task DeviceProductsPrefixesAndWindowsMatchExactCpu(int? period)
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        foreach (int count in new[] { 1, 127, 128, 129, 513 })
        foreach (bool typical in new[] { false, true })
            await Check(Data(count), new VolumeWeightedPrice(period, typical));
    }

    [SkippableTheory]
    [InlineData(null)] [InlineData(1)] [InlineData(20)] [InlineData(int.MaxValue)]
    public async Task NonmonotonicAnchorsCountOnlyParticipatingArrivals(int? period)
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var bars = Data(513);
        for (int i = 0; i < bars.Length; i++)
        {
            var bar = bars[i];
            bars[i] = new(DateTime.UnixEpoch.AddMinutes(i % 19), bar.Open, bar.High, bar.Low, bar.Close, bar.Volume);
        }
        foreach (int offset in new[] { -1, 7, 18, 19 })
        foreach (bool typical in new[] { false, true })
            await Check(bars, new VolumeWeightedPrice(period, typical, DateTime.UnixEpoch.AddMinutes(offset)));
    }

    [SkippableTheory]
    [InlineData(-1074, -1074)] [InlineData(-1040, 900)] [InlineData(-900, -900)]
    [InlineData(0, 0)] [InlineData(900, 900)] [InlineData(1000, -900)]
    public async Task IntegerRoundingHandlesExtremeGridsAndSignedVolumes(int priceScale, int volumeScale)
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var bars = Data(513, priceScale, volumeScale);
        foreach (int? period in new int?[] { null, 1, 2, 17, 127 })
        foreach (bool typical in new[] { false, true })
            await Check(bars, new VolumeWeightedPrice(period, typical));
    }

    [SkippableFact]
    public async Task HalfwaySubnormalRatiosAndExactZeroKeepTheirSigns()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        double e = double.Epsilon;
        Bar[] bars = [new(default, 0, -e, -e, -e, 1), new(default, 0, 0, 0, 0, 1),
            new(default, 0, 0, 0, 0, 1), new(default, 0, -2 * e, -2 * e, -2 * e, 1),
            new(default, -0d, -0d, -0d, -0d, -4), new(default, 0, 3 * e, 3 * e, 3 * e, 1)];
        foreach (int? period in new int?[] { null, 1, 2, 3 })
        foreach (bool typical in new[] { false, true }) await Check(bars, new VolumeWeightedPrice(period, typical));
        var indicator = new VolumeWeightedPrice();
        using var result = await Build(bars, indicator).BuildAsync();
        Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(result[indicator.Value][1]));
        Assert.Equal(0L, BitConverter.DoubleToInt64Bits(result[indicator.Value][4]));
        Assert.Equal(0, result[indicator.IsDefined][4]);
        foreach (int units in new[] { 1, 3, 5, 7 })
            await Check([new(default, 0, -units * e, -units * e, -units * e, 1),
                new(default, 0, 0, 0, 0, 1)], new VolumeWeightedPrice());
        double boundary = Math.ScaleB(1, -1022);
        await Check([new(default, 0, boundary, boundary, boundary, (1L << 39) - 1),
            new(default, 0, 0, 0, 0, 3)], new VolumeWeightedPrice());
    }

    [SkippableFact]
    public async Task LargeCertifiedProductsExerciseTwoLimbRounding()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var bars = Enumerable.Range(0, 257).Select(i => new Bar(default, 0,
            (1 << 20) + i % 31, (1 << 20) - i % 23, (1 << 20) + i % 17,
            (i % 3 == 0 ? -1d : 1d) * ((1 << 21) + i % 19))).ToArray();
        foreach (int? period in new int?[] { null, 1, 2, 127 })
        foreach (bool typical in new[] { false, true }) await Check(bars, new VolumeWeightedPrice(period, typical));
    }

    [SkippableFact]
    public async Task RejectionInputPrecedenceOverflowCancellationAndRecoveryStayExplicit()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var original = Data(257);
        var bars = original.ToArray();
        var indicator = new VolumeWeightedPrice();
        var builder = Build(bars, indicator);
        using var saved = await builder.BuildAsync();
        foreach (var bad in new[]
        {
            new Bar(default, 0, 1, 1, 1, double.Epsilon),
            new Bar(default, 0, double.Epsilon, 0, double.Epsilon, 1),
            new Bar(default, 0, (1L << 40) + 1d, 0, 1, 1),
            new Bar(default, 0, 1, 0, 1, (1L << 40) + 1d)
        })
        {
            bars[0] = bad;
            await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync());
            Assert.Null(builder.LastExecution);
        }
        bars[^1] = new(default, double.NaN, 1, 0, 1, 1);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
        Assert.Null(builder.LastExecution);
        Array.Fill(bars, new Bar(default, 0, (1 << 24) + 1, 0, (1 << 24) + 1, (1 << 24) + 1));
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync());
        Array.Fill(bars, new Bar(default, 0, 0, 0, 0, 0));
        double large = Math.ScaleB(1, 1023);
        bars[0] = new(default, 0, large, large, large, 1);
        bars[1] = new(default, 0, 0, 0, 0, -.5);
        var overflow = await Assert.ThrowsAsync<IndicatorOutputException>(() => builder.BuildAsync());
        Assert.Equal(0, overflow.OutputSlot); Assert.Equal(1, overflow.BarIndex);
        Assert.Null(builder.LastExecution);
        Array.Copy(original, bars, bars.Length);
        using var recovered = await builder.BuildAsync();
        Equal(indicator, saved, recovered);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
        await Assert.ThrowsAsync<NotSupportedException>(() => Build([], indicator).BuildAsync());
        var automaticBuilder = Build(bars, indicator).ConfigureExecution(IndicatorExecutionBackend.Auto);
        using var automatic = await automaticBuilder.BuildAsync();
        Assert.Equal(IndicatorExecutionBackend.Cpu, automaticBuilder.LastExecution!.Backend);
        Equal(indicator, saved, automatic);
    }

    [SkippableFact]
    public async Task WorkspaceReusePreservesEarlierOwnedOutputs()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var bars = Enumerable.Range(0, 257).Select(i =>
            new Bar(DateTime.UnixEpoch.AddMinutes(i), i, 12 + i % 7, 10 + i % 7, 11 + i % 7, i % 13 + 1)).ToArray();
        var weighted = new VolumeWeightedPrice(20);
        using var saved = await Build(bars, weighted).BuildAsync();
        using var expected = await Build(bars, weighted).ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
        foreach (IIndicator other in new IIndicator[]
        {
            new SeededOnBalanceVolume(averagePeriod: 14), weighted, new MoneyFlowWithDetails(14), weighted,
            new AccumulationDistributionWithAverage(14), weighted, new SeededForceIndex(13), weighted,
            new Sma(14), weighted, new BullishCandle(), weighted
        }) await Check(bars, other);
        saved.Dispose();
        Equal(weighted, expected, saved);
    }

    [Fact]
    public void OnlyStandaloneUnsourcedWeightedPriceIsEligible()
    {
        var indicator = new VolumeWeightedPrice();
        Assert.True(ValuesBarExecution.SupportsGpu([indicator]));
        Assert.False(ValuesBarExecution.SupportsGpu([indicator, new Sma(14)]));
        Assert.False(ValuesBarExecution.SupportsGpu([indicator.Of(new Sma(14))]));
    }

    private static Bar[] Data(int count, int priceScale = 0, int volumeScale = 0)
    {
        var random = new Random(91237);
        return Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), i,
            Math.ScaleB(random.Next(-512, 513), priceScale), Math.ScaleB(random.Next(-512, 513), priceScale),
            Math.ScaleB(random.Next(-512, 513), priceScale), Math.ScaleB(random.Next(-1024, 1025), volumeScale))).ToArray();
    }

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
            Array.Clear(input); actual.Dispose();
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

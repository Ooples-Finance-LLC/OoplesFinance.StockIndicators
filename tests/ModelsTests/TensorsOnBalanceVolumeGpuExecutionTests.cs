using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class TensorsOnBalanceVolumeGpuExecutionTests
{
    [SkippableTheory]
    [InlineData(ObvSeed.Zero, null)] [InlineData(ObvSeed.FirstVolume, null)]
    [InlineData(ObvSeed.Zero, 1)] [InlineData(ObvSeed.FirstVolume, 2)]
    [InlineData(ObvSeed.Zero, 14)] [InlineData(ObvSeed.FirstVolume, 127)]
    [InlineData(ObvSeed.Zero, 128)] [InlineData(ObvSeed.FirstVolume, 129)]
    [InlineData(ObvSeed.Zero, int.MaxValue)]
    public async Task DevicePrefixesAndAveragesMatchExactReferenceAcrossBlocks(ObvSeed seed, int? period)
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        foreach (int count in new[] { 1, 127, 128, 129, 513 })
        {
            var bars = Data(count, 0);
            await Check(bars, new SeededOnBalanceVolume(seed, period));
        }
    }

    [SkippableFact]
    public async Task SignedVolumesScaledPricesAndZeroPreserveRounding()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        foreach (int scale in new[] { -400, 400 })
        foreach (var seed in Enum.GetValues<ObvSeed>())
            await Check(Data(513, scale), new SeededOnBalanceVolume(seed, 17));
        await Check([new(default, 0, double.MaxValue, -double.MaxValue, -0d, -0d),
            new(default, 0, 0, 0, 0, 0)], new SeededOnBalanceVolume(averagePeriod: 1));
    }

    [SkippableFact]
    public async Task RetainedColumnsSurvivePackedAndUnpackedWorkspaceReuse()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var bars = Data(257, 0);
        var obv = new SeededOnBalanceVolume(averagePeriod: 14);
        using var expected = await Build(bars, obv).ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
        using var retained = await Build(bars, obv).BuildAsync();
        foreach (IIndicator indicator in new IIndicator[]
        {
            new WindowVortex(14), obv, new Sma(14), obv,
            new SeededOnBalanceVolume(), new SeededOnBalanceVolume(averagePeriod: int.MaxValue), obv
        })
            await Check(bars, indicator);
        retained.Dispose();
        Equal(obv, expected, retained);
    }

    [SkippableFact]
    public async Task RejectionCancellationAndRecoveryPreserveLastExecutionAndOwnership()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var bars = Data(257, 0);
        var original = bars.ToArray();
        var indicator = new SeededOnBalanceVolume(averagePeriod: 14);
        var builder = Build(bars, indicator);
        using var saved = await builder.BuildAsync();
        bars[^1] = new(default, 0, 1, 0, 1, double.MaxValue);
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync());
        Assert.Null(builder.LastExecution);
        bars[^1] = new(default, double.NaN, 1, 0, 1, double.MaxValue);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
        Assert.Null(builder.LastExecution);
        Array.Copy(original, bars, bars.Length);
        using var recovered = await builder.BuildAsync();
        Equal(indicator, saved, recovered);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
        await Assert.ThrowsAsync<NotSupportedException>(() => Build([], indicator).BuildAsync());
        using var automatic = await Build(bars, indicator).ConfigureExecution(IndicatorExecutionBackend.Auto).BuildAsync();
        Equal(indicator, saved, automatic);
    }

    private static Bar[] Data(int count, int scale) => Enumerable.Range(0, count).Select(i =>
    {
        double close = (i / 2 * 13 % 29) - 14;
        return new Bar(DateTime.UnixEpoch.AddMinutes(i), i, close + 1, close - 1, close, Math.ScaleB(i % 17 - 8, scale));
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

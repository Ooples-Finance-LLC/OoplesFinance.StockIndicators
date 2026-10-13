using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class TensorsForceIndexGpuExecutionTests
{
    [SkippableTheory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(13)]
    [InlineData(14)] [InlineData(127)] [InlineData(128)] [InlineData(int.MaxValue)]
    public async Task DeviceSeedAndRecurrenceMatchExactStateAcrossBlocks(int period)
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        foreach (int count in new[] { 1, 14, 127, 128, 129, 257 })
            await Check(Data(count), new SeededForceIndex(period));
    }

    [SkippableFact]
    public async Task SignedScaledInputsAndSubnormalPriceDifferencesRemainExact()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        foreach (var scales in new[] { (0, -400), (0, 400), (-900, 900), (900, -900) })
            await Check(Data(257, scales.Item1, scales.Item2), new SeededForceIndex(13));
        double volume = Math.ScaleB(1, 1023), e = double.Epsilon;
        await Check([new(default, 0, 0, 0, 0, volume), new(default, 0, e, 0, e, volume),
            new(default, 0, 2 * e, 0, 2 * e, -volume), new(default, 0, e, 0, e, volume)], new SeededForceIndex(2));
        // No output is present; the device still supplies both zero columns,
        // without requiring a certificate for unpublished raw products.
        await Check([new(default, 0, 0, 0, double.MaxValue, double.MaxValue),
            new(default, 0, 0, 0, -double.MaxValue, double.Epsilon)], new SeededForceIndex(2));
    }

    [SkippableFact]
    public async Task DeviceRecurrencePreservesSubnormalsAndNegativeUnderflowZero()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var bars = Enumerable.Range(0, 420).Select(i => new Bar(default, 0, 0, 0,
            i == 0 ? 0 : -Math.ScaleB(1, -500), 1)).ToArray();
        var indicator = new SeededForceIndex(2);
        await Check(bars, indicator);
        using var run = await Build(bars, indicator).BuildAsync();
        Assert.Contains(run[indicator.Value].ToArray(), value => double.IsSubnormal(value));
        Assert.Contains(run[indicator.Value].ToArray(), value => BitConverter.DoubleToInt64Bits(value) == long.MinValue);
        Assert.Equal(0L, BitConverter.DoubleToInt64Bits(run[indicator.Value][^1]));
    }

    [SkippableTheory]
    [InlineData(16)] [InlineData(24)] [InlineData(40)]
    public async Task WideSupportedAlignmentUsesBothIntegerLimbs(int pause)
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        foreach (double last in new[] { 0d, 2d })
        {
            var bars = Enumerable.Range(0, pause + 4).Select(i => new Bar(default, 0, 2, 0,
                i == 0 ? 0 : i == pause + 3 ? last : 1, 1)).ToArray();
            // The new unit force and tiny previous EMA require a 78-118 bit
            // aligned numerator, including both signs and a wide divisor grid.
            await Check(bars, new SeededForceIndex(2));
        }
    }

    [SkippableFact]
    public async Task DynamicWideRecurrenceRejectionValidatesInputsFirstAndRecovers()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var bars = Enumerable.Range(0, 200).Select(i => new Bar(default, 0, 2, 0,
            i == 0 ? 0 : i == 199 ? 2 : 1, 1)).ToArray();
        var indicator = new SeededForceIndex(2);
        var builder = Build(bars, indicator);
        // The tiny decayed previous value plus a new unit force needs more than
        // two limbs when aligned. It must reject instead of losing that term.
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync());
        Assert.Null(builder.LastExecution);
        bars[^1] = new(default, double.NaN, 2, 0, 2, 1);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
        Assert.Null(builder.LastExecution);
        Array.Copy(Data(200), bars, bars.Length);
        using var recovered = await builder.BuildAsync();
        Assert.Equal(IndicatorExecutionBackend.Gpu, builder.LastExecution!.Backend);
        using var cpu = await Build(bars, indicator).ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
        Equal(indicator, cpu, recovered);
    }

    [SkippableFact]
    public async Task WorkspaceReuseCancellationAndHostCertificateRejectionKeepOwnedResults()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var bars = Data(257);
        var indicator = new SeededForceIndex(13);
        var builder = Build(bars, indicator);
        using var saved = await builder.BuildAsync();
        using var cpu = await Build(bars, indicator).ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
        foreach (IIndicator next in new IIndicator[] { new WindowVortex(14), indicator,
            new AccumulationDistributionWithAverage(7), indicator, new Sma(14), indicator })
            await Check(bars, next);
        saved.Dispose();
        Equal(indicator, cpu, saved);
        bars[^1] = new(default, 0, 1, 0, 1, double.MaxValue);
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync());
        Assert.Null(builder.LastExecution);
        bars[^1] = new(default, double.NaN, 1, 0, 1, double.MaxValue);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
        Assert.Null(builder.LastExecution);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
        await Assert.ThrowsAsync<NotSupportedException>(() => Build([], indicator).BuildAsync());
        var automatic = Build(Data(257), indicator).ConfigureExecution(IndicatorExecutionBackend.Auto);
        using var auto = await automatic.BuildAsync();
        Assert.Equal(IndicatorExecutionBackend.Cpu, automatic.LastExecution!.Backend);
        Equal(indicator, cpu, auto);
    }

    private static Bar[] Data(int count, int priceScale = 0, int volumeScale = 0) => Enumerable.Range(0, count).Select(i =>
    {
        double close = Math.ScaleB(10 + (i / 2 * 13 % 101) / 8d, priceScale);
        double spread = Math.ScaleB(1, priceScale);
        return new Bar(DateTime.UnixEpoch.AddMinutes(i), i, close + spread, close - spread, close,
            Math.ScaleB(i % 17 - 8, volumeScale));
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

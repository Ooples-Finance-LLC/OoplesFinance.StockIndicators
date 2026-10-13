using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class TensorsSeededAtrGpuExecutionTests
{
    [SkippableTheory]
    [InlineData(1)] [InlineData(2)] [InlineData(20)]
    [InlineData(127)] [InlineData(128)] [InlineData(int.MaxValue)]
    public async Task AllDeviceColumnsMatchIndependentReferenceAcrossSeedsAndBlocks(int period)
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        foreach (int count in new[] { 1, 19, 20, 21, 127, 128, 129, 257 })
            await Check(Data(count), period);
    }

    [SkippableTheory]
    [InlineData(1)] [InlineData(2)] [InlineData(20)] [InlineData(127)]
    public async Task SignedInvertedAndScaledRangesRetainExactRecurrence(int period)
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        foreach (int scale in new[] { -400, 0, 400 })
        {
            var random = new Random(9124 + period);
            var bars = Enumerable.Range(0, 1031).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0,
                Math.ScaleB(random.Next(-(1 << 18), 1 << 18), scale),
                Math.ScaleB(random.Next(-(1 << 18), 1 << 18), scale),
                Math.ScaleB(i % 11 == 0 ? 0 : random.Next(-(1 << 18), 1 << 18), scale), 0)).ToArray();
            await Check(bars, period);
        }
    }

    [SkippableFact]
    public async Task DecayingAverageAndPercentPreserveSubnormalsAndNegativeUnderflowZero()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var bars = Enumerable.Range(0, 1100).Select(i => new Bar(default, 0,
            i == 1 ? -1023 : -1024, -1024, -1024, 0)).ToArray();
        await Check(bars, 2);
        var indicator = new AverageTrueRangeWithDetails(2);
        using var run = await Build(bars, indicator).BuildAsync();
        Assert.Contains(run[indicator.Average].ToArray(), double.IsSubnormal);
        Assert.Contains(run[indicator.Percent].ToArray(), value => BitConverter.DoubleToInt64Bits(value) == long.MinValue);
        Assert.Equal(0L, BitConverter.DoubleToInt64Bits(run[indicator.Percent][^1]));
        await Check(Enumerable.Repeat(new Bar(default, 0, -0d, 0, -0d, 0), 129).ToArray(), 20);
    }

    [SkippableFact]
    public async Task WideRecurrenceRejectsWithoutLosingTermsAndRecovers()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var bars = Enumerable.Range(0, 200).Select(i => new Bar(default, 0,
            i == 1 || i == 199 ? 2 : 1, 1, 1, 0)).ToArray();
        foreach (IIndicator indicator in Family(2))
        {
            var builder = Build(bars, indicator);
            await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync());
            Assert.Null(builder.LastExecution);
            var last = bars[^1];
            bars[^1] = new(default, double.NaN, last.High, last.Low, last.Close, 0);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
            Assert.Null(builder.LastExecution);
            bars[^1] = last;
            using var recovered = await Build(Data(200), indicator).BuildAsync();
            using var cpu = await Build(Data(200), indicator).ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
            Equal(indicator, cpu, recovered);
        }
    }

    [SkippableFact]
    public async Task WorkspaceReuseAndRejectionPreserveOwnershipAndBackendContracts()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var bars = Data(257);
        var indicator = new AverageTrueRangeWithDetails(20);
        var builder = Build(bars, indicator);
        using var saved = await builder.BuildAsync();
        using var cpu = await Build(bars, indicator).ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
        foreach (IIndicator next in new IIndicator[] { new SeededForceIndex(13), indicator,
            new VolumeWeightedPrice(), indicator, new Sma(14), indicator, new NormalizedSeededAverageTrueRange(20) })
        {
            using var run = await Build(bars, next).BuildAsync();
            using var expected = await Build(bars, next).ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
            Equal(next, expected, run);
        }
        Array.Clear(bars); saved.Dispose(); Equal(indicator, cpu, saved);
        bars[^1] = new(default, 0, double.MaxValue, -double.MaxValue, 1, 0);
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync());
        Assert.Null(builder.LastExecution);
        bars[^1] = new(default, 0, double.MaxValue, -double.MaxValue, 1, double.NaN);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
        await Assert.ThrowsAsync<NotSupportedException>(() => Build([], indicator).BuildAsync());
        var automatic = Build(Data(257), indicator).ConfigureExecution(IndicatorExecutionBackend.Auto);
        using var auto = await automatic.BuildAsync();
        Assert.Equal(IndicatorExecutionBackend.Cpu, automatic.LastExecution!.Backend);
        Equal(indicator, cpu, auto);
    }

    private static Bar[] Data(int count) => Enumerable.Range(0, count).Select(i =>
    {
        double close = 10 + (i * 13 % 101) / 8d;
        return new Bar(DateTime.UnixEpoch.AddMinutes(i), i, close + (i % 3 + 1) / 4d,
            close - .5, i % 17 == 0 ? 0 : close, i % 17 + 1);
    }).ToArray();

    private static IIndicator[] Family(int period) => [new SeededAverageTrueRange(period),
        new NormalizedSeededAverageTrueRange(period), new AverageTrueRangeWithDetails(period)];

    private static async Task Check(Bar[] bars, int period)
    {
        var expected = SeededAtrReference.Values(bars, period);
        foreach (IIndicator indicator in Family(period))
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var input = bars.ToArray();
            var builder = Build(input, indicator).ConfigureHistory(history);
            using var actual = await builder.BuildAsync();
            Assert.Equal(IndicatorExecutionBackend.Gpu, builder.LastExecution!.Backend);
            Array.Clear(input); actual.Dispose();
            for (int slot = 0; slot < indicator.Outputs.Count; slot++)
            {
                int referenceSlot = indicator is AverageTrueRangeWithDetails ? slot
                    : indicator is NormalizedSeededAverageTrueRange ? 2 : 1;
                Assert.Equal(expected[referenceSlot].Select(BitConverter.DoubleToInt64Bits),
                    actual[indicator.Outputs[slot]].ToArray().Select(BitConverter.DoubleToInt64Bits));
            }
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

    private static StockIndicatorBuilder Build(Bar[] bars, IIndicator indicator) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).ConfigureExecution(IndicatorExecutionBackend.Gpu);
}

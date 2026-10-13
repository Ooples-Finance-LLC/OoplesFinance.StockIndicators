using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class ScaledTrueRangeSharedExecutionTests
{
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(20)] [InlineData(int.MaxValue)]
    public async Task CpuWorkersPreserveSingleRoundingAcrossPartitions(int divisor)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int count in new[] { 0, 1, 511, 512, 513, 8193 })
            {
                var random = new Random(2158);
                var bars = Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i),
                    i, 10 + random.NextDouble(), 8 + random.NextDouble(), 9 + random.NextDouble(), i)).ToArray();
                foreach (int workers in new[] { 1, 8 })
                {
                    CpuParallelSettings.MaxDegreeOfParallelism = workers;
                    await Check(bars, divisor, IndicatorExecutionBackend.Cpu);
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [SkippableTheory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(20)] [InlineData(int.MaxValue)]
    public async Task DeviceQuotientsPreserveGridsSignedRangesAndMaximumDivisors(int divisor)
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        foreach (int scale in new[] { -1074, -1070, -1022, -500, 0, 500, 1000 })
        {
            var random = new Random(741);
            var bars = Enumerable.Range(0, 257).Select(i => new Bar(default, 0,
                Math.ScaleB(random.Next(-1024, 1024), scale), Math.ScaleB(random.Next(-1024, 1024), scale),
                Math.ScaleB(random.Next(-1024, 1024), scale), 0)).ToArray();
            await Check(bars, divisor, IndicatorExecutionBackend.Gpu);
        }
        await Check([new(default, 0, -double.Epsilon, 0, 0, 0)], divisor, IndicatorExecutionBackend.Gpu);
        await Check([new(default, 0, -0d, 0, -0d, 0), new(default, 0, 0, -0d, 0, 0)], divisor, IndicatorExecutionBackend.Gpu);
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(20)]
    public async Task CpuWideDifferencesRetainOneFinalDivision(int divisor)
    {
        // Large span, inverted first range, and cancellation across binary grids.
        double m = double.MaxValue;
        Bar[] bars = [new(default, 0, -1, 1, 0, 0), new(default, 0, m, -m, 1, 0),
            new(default, 0, double.Epsilon, -double.Epsilon, -m, 0), new(default, 0, 1, -1, 0, 0)];
        if (divisor == 1)
            await Assert.ThrowsAsync<IndicatorOutputException>(() => Build(bars, new(divisor), IndicatorExecutionBackend.Cpu).BuildAsync());
        else await Check(bars, divisor, IndicatorExecutionBackend.Cpu);
    }

    [SkippableFact]
    public async Task DeviceScalesBeforeOverflowAndRetainsNegativeUnderflowZero()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        double h = Math.ScaleB(1, 1023);
        await Check([new(default, 0, h, -h, h, 0), new(default, 0, -h, h, -h, 0)], 2, IndicatorExecutionBackend.Gpu);
        var indicator = new ScaledTrueRange(2);
        using var run = await Build([new(default, 0, -double.Epsilon, 0, 0, 0)], indicator, IndicatorExecutionBackend.Gpu).BuildAsync();
        Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(run[indicator.Value][0]));
    }

    [SkippableTheory]
    [InlineData(IndicatorExecutionBackend.Cpu)] [InlineData(IndicatorExecutionBackend.Gpu)]
    public async Task ValidationPrecedesOutputOverflowAndCancellationDoesNotPoisonRecovery(IndicatorExecutionBackend backend)
    {
        if (backend == IndicatorExecutionBackend.Gpu) Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        double h = Math.ScaleB(1, 1023);
        var bars = Enumerable.Repeat(new Bar(default, 0, h, -h, 0, 0), 8193).ToArray();
        var indicator = new ScaledTrueRange(1);
        var builder = Build(bars, indicator, backend);
        bars[^1] = new(default, 0, h, -h, 0, double.NaN);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
        bars[^1] = bars[0];
        var error = await Assert.ThrowsAsync<IndicatorOutputException>(() => builder.BuildAsync());
        Assert.Equal(0, error.OutputSlot); Assert.Equal(0, error.BarIndex);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Array.Fill(bars, new Bar(default, 0, 1, 0, 1, 0));
        using var recovered = await builder.BuildAsync();
        Assert.All(recovered[indicator.Value].ToArray(), v => Assert.Equal(1d, v));
    }

    [SkippableFact]
    public async Task DeviceRejectsUnsupportedGridsAndAutoStaysCpu()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var indicator = new ScaledTrueRange(2);
        var bars = new[] { new Bar(default, 0, double.MaxValue, -double.MaxValue, 0, 0) };
        var builder = Build(bars, indicator, IndicatorExecutionBackend.Gpu);
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync());
        Assert.Null(builder.LastExecution);
        await Assert.ThrowsAsync<NotSupportedException>(() => Build([], indicator, IndicatorExecutionBackend.Gpu).BuildAsync());
        var autoBuilder = Build(bars, indicator, IndicatorExecutionBackend.Auto);
        using var automatic = await autoBuilder.BuildAsync();
        Assert.Equal(IndicatorExecutionBackend.Cpu, autoBuilder.LastExecution!.Backend);
        Assert.Equal(double.MaxValue, automatic[indicator.Value][0]);
    }

    private static async Task Check(Bar[] bars, int divisor, IndicatorExecutionBackend backend)
    {
        var expected = Reference(bars, divisor);
        var indicator = new ScaledTrueRange(divisor);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var input = bars.ToArray();
            var builder = Build(input, indicator, backend).ConfigureHistory(history);
            using var run = await builder.BuildAsync();
            Assert.Equal(backend, builder.LastExecution!.Backend);
            Array.Clear(input); run.Dispose();
            Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), run[indicator.Value].ToArray().Select(BitConverter.DoubleToInt64Bits));
            if (bars.Length > 0) Assert.Equal(bars[^1], run.Latest.Bar);
            if (history == IndicatorHistoryMode.Full)
            {
                int row = 0;
                await foreach (var snapshot in run) Assert.Equal(bars[row++], snapshot.Bar);
                Assert.Equal(bars.Length, row);
            }
        }
    }

    private static double[] Reference(Bar[] bars, int divisor) => bars.Select((bar, i) =>
    {
        var high = ReferenceFraction.FromDouble(bar.High);
        var low = ReferenceFraction.FromDouble(bar.Low);
        var range = high - low;
        if (i > 0)
        {
            var previous = ReferenceFraction.FromDouble(bars[i - 1].Close);
            range = new[] { range, (high - previous).Abs(), (low - previous).Abs() }.Max();
        }
        return (range / new ReferenceFraction(divisor)).ToDouble();
    }).ToArray();

    private static StockIndicatorBuilder Build(Bar[] bars, ScaledTrueRange indicator, IndicatorExecutionBackend backend) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).ConfigureExecution(backend);
}

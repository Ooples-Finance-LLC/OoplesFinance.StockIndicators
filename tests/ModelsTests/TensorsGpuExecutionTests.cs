using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class TensorsGpuExecutionTests
{
    private static Bar[] BarsFor(IEnumerable<double> values) => values.Select((x, i) =>
        new Bar(DateTime.UnixEpoch.AddMinutes(i), x, x, x, x, 1)).ToArray();

    private static void RequireGpu()
    {
        bool available = TensorsGpuExecution.TryGet(out _, out string reason);
        Skip.IfNot(available, reason);
    }

    [SkippableTheory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(20)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(4096)]
    [InlineData(5000)]
    public async Task FusedGpuMeanMatchesIndependentRationalWindowsAndComposedAsin(int period)
    {
        RequireGpu();
        var close = Enumerable.Range(0, period == 4096 ? 4101 : 2051).Select(i => (i % 17 - 8) / 16d).ToArray();
        close[0] = -0d;
        var bars = BarsFor(close);
        var sma = new Sma(period);
        var asin = new PriceCircularTransform(PriceCircularOperation.ArcSine);
        asin.Of(sma);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(sma, asin).ConfigureExecution(IndicatorExecutionBackend.Gpu);
        using var run = await builder.BuildAsync();
        Assert.Equal(IndicatorExecutionBackend.Gpu, builder.LastExecution!.Backend);
        Assert.False(string.IsNullOrWhiteSpace(builder.LastExecution.DeviceName));
        var actual = run[sma].ToArray();
        for (var i = 0; i < close.Length; i++)
        {
            var sum = new ReferenceFraction(0);
            if (i >= period - 1)
                for (var j = i - period + 1; j <= i; j++) sum += ReferenceFraction.FromDouble(close[j]);
            var expected = period == 1 ? close[i] : (sum / new ReferenceFraction(period)).ToDouble();
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual[i]));
            AssertAsin(Math.Asin(expected), run[asin.Value][i]);
            Assert.Equal(1d, run[asin.IsDefined][i]);
        }
        // Only the downstream outputs are requested on this second run.
        var dependentBuilder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(asin).ConfigureExecution(IndicatorExecutionBackend.Gpu);
        using var dependent = await dependentBuilder.BuildAsync();
        Assert.Equal(run[asin.Value].ToArray(), dependent[asin.Value].ToArray());
        Assert.Throws<KeyNotFoundException>(() => dependent[sma].ToArray());
        Array.Clear(bars);
        Assert.Equal(actual, run[sma].ToArray());
    }

    [SkippableFact]
    public async Task GpuAsinKeepsDomainsSignsAndIndependentAccuracyBudget()
    {
        RequireGpu();
        var random = new Random(871);
        var close = new[] { -0d, 0d, -1d, 1d, Math.BitIncrement(-1d), Math.BitDecrement(1d),
            Math.BitDecrement(-1d), Math.BitIncrement(1d), double.Epsilon, -double.Epsilon,
            1e-300, -1e-300, double.MaxValue, -double.MaxValue }
            .Concat(Enumerable.Range(0, 129).Select(_ => random.NextDouble() * 2 - 1)).ToArray();
        var asin = new PriceCircularTransform(PriceCircularOperation.ArcSine);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(BarsFor(close)))
            .ConfigureIndicators(asin).ConfigureExecution(IndicatorExecutionBackend.Gpu);
        using var run = await builder.BuildAsync();
        Assert.Equal(IndicatorExecutionBackend.Gpu, builder.LastExecution!.Backend);
        for (var i = 0; i < close.Length; i++)
        {
            bool defined = close[i] is >= -1 and <= 1;
            var expected = !defined ? 0 : close[i] == 0 ? close[i]
                : CircularReference.Value(close[i], PriceCircularOperation.ArcSine);
            AssertAsin(expected, run[asin.Value][i]);
            Assert.Equal(defined ? 1d : 0d, run[asin.IsDefined][i]);
        }
    }

    [SkippableFact]
    public async Task UncertifiedSmaIsRejectedWhenGpuIsRequiredAndAutoUsesCpu()
    {
        RequireGpu();
        var close = Enumerable.Repeat(.25d, 100_001).ToArray();
        close[^1] = double.Epsilon;
        var bars = BarsFor(close);
        var sma = new Sma(20);
        var asin = new PriceCircularTransform(PriceCircularOperation.ArcSine);
        asin.Of(sma);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(asin, sma)
            .ConfigureExecution(IndicatorExecutionBackend.Gpu);
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync());
        Assert.Null(builder.LastExecution);
        using var automatic = await builder.ConfigureExecution(IndicatorExecutionBackend.Auto).BuildAsync();
        Assert.Equal(IndicatorExecutionBackend.Cpu, builder.LastExecution!.Backend);
        using var cpu = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(asin, sma)
            .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
        Assert.Equal(cpu[sma].ToArray(), automatic[sma].ToArray());
        Assert.Equal(cpu[asin.Value].ToArray(), automatic[asin.Value].ToArray());
    }

    [SkippableFact]
    public async Task ConcurrentGpuBuildsKeepSeparateArgumentsAndResults()
    {
        RequireGpu();
        var results = await Task.WhenAll(Enumerable.Range(2, 5).Select(period => Task.Run(async () =>
        {
            var sma = new Sma(period);
            int count = 1023 + period;
            var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(BarsFor(Enumerable.Repeat((double)period, count))))
                .ConfigureIndicators(sma).ConfigureExecution(IndicatorExecutionBackend.Gpu);
            using var run = await builder.BuildAsync();
            Assert.Equal(IndicatorExecutionBackend.Gpu, builder.LastExecution!.Backend);
            Assert.Equal(count, run[sma].ToArray().Length);
            return (Period: period, Output: run[sma].ToArray());
        })));
        foreach (var result in results)
        {
            Assert.All(result.Output.Take(result.Period - 1), value => Assert.Equal(0, value));
            Assert.All(result.Output.Skip(result.Period - 1), value => Assert.Equal(result.Period, value));
        }
    }

    [Fact]
    public async Task RequiredGpuDoesNotSilentlyExecuteAnUnsupportedSourceOnCpu()
    {
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(BarsFor(new[] { .5d }), bar => bar))
            .ConfigureIndicators(new Sma(20)).ConfigureExecution(IndicatorExecutionBackend.Gpu);
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync());
        Assert.Null(builder.LastExecution);
    }

    [SkippableFact]
    public async Task FailedGpuRunsClearDiagnosticsAndPreserveEarlierResults()
    {
        RequireGpu();
        var sma = new Sma(1);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(BarsFor(new[] { .5d })))
            .ConfigureIndicators(sma).ConfigureExecution(IndicatorExecutionBackend.Gpu);
        using var previous = await builder.BuildAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancellation.Token));
        Assert.Null(builder.LastExecution);
        builder.ConfigureSource(Bars.From(BarsFor(new[] { double.NaN })));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => builder.BuildAsync());
        Assert.Null(builder.LastExecution);
        Assert.Equal(.5d, previous[sma][0]);
    }

    [Fact]
    public async Task RequiredGpuRejectsEmptyRunsAndLegacyBuild()
    {
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(Array.Empty<Bar>()))
            .ConfigureIndicators(new Sma(20)).ConfigureExecution(IndicatorExecutionBackend.Gpu);
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.BuildAsync());
        Assert.Throws<NotSupportedException>(() => builder.Build());
        Assert.Null(builder.LastExecution);
    }

    [SkippableFact]
    public async Task ReusedWorkspaceHandlesChangingDomainsShapesSizesAndOwnedSnapshots()
    {
        RequireGpu();
        // Same-size runs alternate between host-proven and device-computed flags.
        // Cross capacity boundaries, then reuse only a prefix of a larger rental.
        // Earlier runs remain readable after disposal and subsequent reuse.
        foreach (var count in new[] { 32767, 32768, 32769, 32770, 2051, 32769 })
        foreach (var mode in new[] { 0, 1, 2, 3 })
        {
            var values = Enumerable.Range(0, count).Select(i =>
                mode == 0 ? .5d : (i % 31 - 15) / 4d).ToArray();
            var bars = BarsFor(values);
            var sma = new Sma(3);
            var asin = new PriceCircularTransform(PriceCircularOperation.ArcSine);
            if (mode != 2) asin.Of(sma);
            IIndicator[] configured = mode == 3 ? new IIndicator[] { sma } : new IIndicator[] { sma, asin };
            var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(configured).ConfigureExecution(IndicatorExecutionBackend.Gpu);
            using var gpu = await builder.BuildAsync();
            Assert.Equal(IndicatorExecutionBackend.Gpu, builder.LastExecution!.Backend);
            using var cpu = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(configured).ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
            Assert.Equal(cpu[sma].ToArray(), gpu[sma].ToArray());
            if (mode != 3)
            {
                Assert.Equal(cpu[asin.IsDefined].ToArray(), gpu[asin.IsDefined].ToArray());
                for (var i = 0; i < count; i++) AssertAsin(cpu[asin.Value][i], gpu[asin.Value][i]);
            }
            var saved = gpu[sma].ToArray();
            var expectedBars = bars.ToArray();
            Array.Clear(bars);
            gpu.Dispose();
            using var overwrite = await new StockIndicatorBuilder()
                .ConfigureSource(Bars.From(BarsFor(Enumerable.Repeat(.25d, count))))
                .ConfigureIndicators(new Sma(1)).ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync();
            Assert.Equal(saved, gpu[sma].ToArray());
            var snapshots = new List<Bar>();
            await foreach (var snapshot in gpu) snapshots.Add(snapshot.Bar);
            Assert.Equal(expectedBars, snapshots);
        }
    }

    private static void AssertAsin(double expected, double actual)
    {
        Assert.True(double.IsFinite(actual));
        if (expected == 0)
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
        else
        {
            Assert.Equal(Math.Sign(expected), Math.Sign(actual));
            Assert.True(Math.Abs(actual - expected) / Math.Abs(expected) <= 4e-15,
                $"Asin relative error exceeded its contract: expected {expected:R}, actual {actual:R}");
        }
    }
}

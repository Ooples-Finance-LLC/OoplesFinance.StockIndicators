using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class IndicatorValuesTests
{
    public static IEnumerable<object[]> Cases => Enumerable.Range(0, 8)
        .SelectMany(kind => new[] { 0, 1, 2 }.Select(data => new object[] { kind, data }));
    private static IIndicator Indicator(int kind) => kind switch
    {
        0 => new Sma(20), 1 => new PriceCircularTransform(PriceCircularOperation.ArcSine),
        2 => new JurikAdaptive(), 3 => new ScaledTrueRange(), 4 => new RollingPivotLevels(),
        5 => new RetrospectiveFractals(3, 5), 6 => new RickshawManCandle(),
        _ => new BullishShortBodyCandle()
    };
    private static Bar[] Data(int count, int mode = 0) => Enumerable.Range(0, count).Select(i =>
    {
        double close = mode == 1 ? 100 + i % 19 / 100d : (i % 127 - 63) / 64d;
        if (mode == 2 && i == count - 1) close = double.Epsilon;
        if (i == 0 && mode != 1) close = -0d;
        return new Bar(DateTime.UnixEpoch.AddMinutes(i), close - .125, close + .25, close - .25, close, 1);
    }).ToArray();
    private static StockIndicatorBuilder Builder(Bar[] bars, params IIndicator[] indicators) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicators);

    [Theory, MemberData(nameof(Cases))]
    public async Task EightPilotsPreserveEveryOutputAndOwnOnlyResults(int kind, int data)
    {
        var bars = Data(2051, data);
        var indicator = Indicator(kind);
        var builder = Builder(bars, indicator);
        using var history = await Builder(bars, indicator).BuildAsync();
        var values = await builder.BuildValuesAsync();
        Assert.Equal(history.BarCount, values.BarCount);
        var saved = indicator.Outputs.Select(o => values[o].ToArray()).ToArray();
        for (int slot = 0; slot < saved.Length; slot++) Bits(history[indicator.Outputs[slot]].ToArray(), saved[slot]);
        Array.Clear(bars);
        _ = await builder.BuildValuesAsync();
        history.Dispose();
        for (int slot = 0; slot < saved.Length; slot++) Bits(saved[slot], values[indicator.Outputs[slot]].ToArray());
        Assert.Throws<KeyNotFoundException>(() => values[new Sma()].ToArray());
    }

    [Fact]
    public async Task MixedPeriodsAndDuplicateConfigurationPreserveIdentity()
    {
        var bars = Data(137);
        var indicators = Enumerable.Range(0, 8).Select(Indicator).Append(new Sma(50)).ToArray();
        var builder = Builder(bars, indicators.Concat(new[] { indicators[0] }).ToArray());
        using var expected = await Builder(bars, indicators).BuildAsync();
        var actual = await builder.BuildValuesAsync();
        foreach (var indicator in indicators)
            foreach (var output in indicator.Outputs) Bits(expected[output].ToArray(), actual[output].ToArray());
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(19)]
    public async Task EmptyAndIncompleteWindows(int count)
    {
        var indicators = new IIndicator[] { new Sma(int.MaxValue), new RetrospectiveFractals(int.MaxValue, 2) };
        var builder = Builder(Data(count), indicators);
        var actual = await builder.BuildValuesAsync();
        Assert.Equal(count, actual.BarCount);
        foreach (var indicator in indicators)
            foreach (var output in indicator.Outputs) Assert.All(actual[output].ToArray(), x => Assert.Equal(0, x));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task WarmupAndComposedGraphsAutomaticallyUseCompatibleExecution(bool warmup)
    {
        var sma = new Sma(3);
        var asin = new PriceCircularTransform(PriceCircularOperation.ArcSine);
        asin.Of(sma);
        IBarSource source = Bars.From(Data(65), b => b);
        if (warmup) source = source.WarmedWith(Data(11));
        var builder = new StockIndicatorBuilder().ConfigureSource(source).ConfigureIndicators(asin, new Ema(7));
        using var expected = await builder.BuildAsync();
        var actual = await builder.BuildValuesAsync();
        Bits(expected[asin.Value].ToArray(), actual[asin.Value].ToArray());
        Bits(expected[asin.IsDefined].ToArray(), actual[asin.IsDefined].ToArray());
        Assert.Equal(65, actual.BarCount);
        Assert.Throws<KeyNotFoundException>(() => actual[sma].ToArray());
    }

    [Fact]
    public async Task InvalidInputCancellationAndLiveSourcesCannotPublishValues()
    {
        var bars = Data(40);
        var builder = Builder(bars, new Sma(3));
        _ = await builder.BuildValuesAsync();
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildValuesAsync(cancellation.Token));
        Assert.Null(builder.LastExecution);
        bars[^1] = new Bar(default, double.NaN, 1, 0, .5, 1);
        await Assert.ThrowsAnyAsync<ArgumentException>(() => builder.BuildValuesAsync());
        Assert.Null(builder.LastExecution);
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.ConfigureSource(Bars.Live()).BuildValuesAsync());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RawValidationPrecedesUnrepresentableArithmetic(bool invalidTail)
    {
        var bars = new[] { new Bar(default, 0, double.MaxValue, -double.MaxValue, 0, 1),
            new Bar(default, 0, 1, 0, 0, invalidTail ? double.NaN : 1) };
        var builder = Builder(bars, new ScaledTrueRange(1));
        var expected = await Record.ExceptionAsync(() => Builder(bars, new ScaledTrueRange(1)).BuildAsync());
        var actual = await Record.ExceptionAsync(() => builder.BuildValuesAsync());
        Assert.NotNull(expected);
        Assert.NotNull(actual);
        Assert.Equal(expected.GetType(), actual.GetType());
        Assert.Null(builder.LastExecution);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PilotAllocationsExcludeFullBarHistory(bool sma)
    {
        var builder = Builder(Data(10_000), sma ? new Sma(20) : new PriceCircularTransform(PriceCircularOperation.ArcSine));
        _ = builder.BuildValuesAsync().GetAwaiter().GetResult();
        long before = GC.GetAllocatedBytesForCurrentThread();
        var result = builder.BuildValuesAsync().GetAwaiter().GetResult();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        // SMA owns one output plus a temporary close column. Asin owns values and flags.
        Assert.InRange(allocated, 160_000, 200_000);
        GC.KeepAlive(result);
    }

    private static void Bits(double[] expected, double[] actual) => Assert.Equal(
        expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));

    [SkippableFact]
    public async Task RequiredGpuStillRunsOnGpuAndReturnsIndependentValues()
    {
        Skip.IfNot(TensorsGpuExecution.TryGet(out _, out var reason), reason);
        var bars = Data(1031);
        var sma = new Sma(3);
        var builder = Builder(bars, sma).ConfigureExecution(IndicatorExecutionBackend.Gpu);
        var values = await builder.BuildValuesAsync();
        Assert.Equal(IndicatorExecutionBackend.Gpu, builder.LastExecution!.Backend);
        using var expected = await Builder(bars, sma).BuildAsync();
        Array.Clear(bars);
        Bits(expected[sma].ToArray(), values[sma].ToArray());
    }
}

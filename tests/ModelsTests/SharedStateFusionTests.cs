using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedStateFusionTests
{
    public static IEnumerable<object[]> Cases =>
        from family in Enumerable.Range(0, 10)
        from period in new[] { 2, 7, int.MaxValue }
        from count in new[] { 0, 1, 41 }
        select new object[] { family, period, count };

    private static IIndicator Create(int family, int period) => family switch
    {
        0 => new FirstValueEma(period),
        1 => new NormalizedConvolution(new[] { 1d, -2d, 3d, 4d }),
        2 => new WindowLinearRegression(period),
        3 => new WindowDispersion(period),
        4 => new EndpointWeightedAverage(period),
        5 => new GaussianWeightedAverage(period),
        6 => new SineWeightedAverage(period),
        7 => new StandardDeviationWithDetails(period, 3),
        8 => new WindowDeviationBands(period),
        _ => new ClassicDeviationBands(period)
    };

    private static Bar[] Input(int count) => Enumerable.Range(0, count)
        .Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 10, 12, 8, 10 + (i % 9) / 8d, i + 1)).ToArray();

    private static StockIndicatorBuilder Builder(Bar[] bars, IIndicator indicator, IndicatorHistoryMode history) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).ConfigureHistory(history);

    [Theory, MemberData(nameof(Cases))]
    public async Task FusedStatesPreserveBitsAndOwnPublishedHistory(int family, int period, int count)
    {
        var bars = Input(count);
        var indicator = Create(family, period);
        using var reference = await Builder(bars, indicator, IndicatorHistoryMode.Full).ConfigureBehavior(_ => { }).BuildAsync();
        var builder = Builder(bars, indicator, IndicatorHistoryMode.Full);
        using var full = await builder.BuildAsync();
        using var latest = await Builder(bars, indicator, IndicatorHistoryMode.LatestOnly).BuildAsync();
        Assert.Contains("Fused CPU values", builder.LastExecution!.Reason);
        Assert.False(((IndicatorRun)full).HasLegacyRuntime);
        Array.Clear(bars);
        foreach (var output in indicator.Outputs)
        {
            Bits(reference[output].ToArray(), full[output].ToArray());
            Bits(reference[output].ToArray(), latest[output].ToArray());
        }
        var original = Input(count);
        var index = 0;
        await foreach (var snapshot in full) Assert.Equal(original[index++], snapshot.Bar);
        Assert.Equal(count, index);
        if (count != 0) Assert.Equal(original[^1], latest.Latest.Bar);
        full.Dispose();
        latest.Dispose();
        Bits(reference[indicator].ToArray(), full[indicator].ToArray());
        Bits(reference[indicator].ToArray(), latest[indicator].ToArray());

        SeriesHandle price = default;
        using var runtime = builder.ConfigureIndicators(catalog => price = catalog.Price()).Build();
        runtime.Start();
        Bits(original.Select(b => b.Close).ToArray(), runtime.GetSeries(price).ToArray());
    }

    [Fact]
    public async Task MixedStatesAndDuplicateRootsShareOneValidatedPass()
    {
        var bars = Input(41);
        var indicators = Enumerable.Range(0, 10).Select(i => Create(i, 7)).ToArray();
        foreach (var history in new[] { IndicatorHistoryMode.Full, IndicatorHistoryMode.LatestOnly })
        {
            using var reference = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(indicators).ConfigureBehavior(_ => { }).ConfigureHistory(history).BuildAsync();
            using var fused = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(indicators.Concat(indicators)).ConfigureHistory(history).BuildAsync();
            foreach (var indicator in indicators)
            foreach (var output in indicator.Outputs) Bits(reference[output].ToArray(), fused[output].ToArray());
        }
    }

    [Theory]
    [InlineData(IndicatorHistoryMode.Full)]
    [InlineData(IndicatorHistoryMode.LatestOnly)]
    public async Task RawValidationPrecedesOutputOverflowAndFailedRunsRecover(IndicatorHistoryMode history)
    {
        var bars = new[]
        {
            new Bar(default, 1, 2, 0, double.MaxValue, 1),
            new Bar(default, 1, 2, 0, -double.MaxValue, 1),
            new Bar(default, 1, 2, 0, 1, double.NaN)
        };
        var indicator = new WindowDispersion(2, WindowDispersionOutput.Variance);
        var builder = Builder(bars, indicator, history);
        var reference = Builder(bars, indicator, history).ConfigureBehavior(_ => { });
        var expected = await Record.ExceptionAsync(() => reference.BuildAsync());
        var actual = await Record.ExceptionAsync(() => builder.BuildAsync());
        Assert.IsType<ArgumentOutOfRangeException>(actual);
        Assert.Equal(expected!.Message, actual.Message);
        Assert.Null(builder.LastExecution);
        bars[^1] = new Bar(default, 1, 2, 0, 1, 1);
        expected = await Record.ExceptionAsync(() => reference.BuildAsync());
        actual = await Record.ExceptionAsync(() => builder.BuildAsync());
        Assert.NotNull(actual);
        Assert.Equal(expected!.GetType(), actual.GetType());
        Assert.Equal(expected.Message, actual.Message);
        Assert.Null(builder.LastExecution);
        Array.Fill(bars, bars[^1]);
        using var recovered = await builder.BuildAsync();
        Assert.Equal(3, recovered.BarCount);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancelled.Token));
        Assert.Null(builder.LastExecution);
    }

    [Fact]
    public void LatestOnlyAvoidsAllocatingTheFullOhlcvHistory()
    {
        const int count = 4096;
        var bars = Input(count);
        var indicator = new FirstValueEma(7);
        long Allocated(IndicatorHistoryMode history)
        {
            var builder = Builder(bars, indicator, history);
            var before = GC.GetAllocatedBytesForCurrentThread();
            using var run = builder.BuildAsync().GetAwaiter().GetResult();
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
        for (var i = 0; i < 4; i++) { Allocated(IndicatorHistoryMode.Full); Allocated(IndicatorHistoryMode.LatestOnly); }
        var retained = Allocated(IndicatorHistoryMode.Full) - Allocated(IndicatorHistoryMode.LatestOnly);
        Assert.InRange(retained, 48L * count, 48L * count + 8192);
    }

    private static void Bits(double[] expected, double[] actual) =>
        Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
}

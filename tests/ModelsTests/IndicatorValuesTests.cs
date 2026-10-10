using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class IndicatorValuesTests
{
    [Fact]
    public async Task ConcurrentSmaBuildsCannotChangePreviouslyPublishedSeriesThroughScratchReuse()
    {
        var completed = await Task.WhenAll(Enumerable.Range(0, 8).Select(k => Task.Run(async () =>
        {
            var bars = Enumerable.Range(0, 65537 + k)
                .Select(i => new Bar(default, 1, 1, 1, 100 + k + i % 19 / 100d, 1)).ToArray();
            var sma = new Sma(k % 2 == 0 ? 20 : 1000);
            var expected = new double[bars.Length];
            OoplesFinance.StockIndicators.Core.MovingAverageCore.SimpleMovingAverage(
                bars.Select(b => b.Close).ToArray(), expected, sma.Length);
            var run = await Builder(bars, sma).ConfigureHistory(k % 2 == 0
                ? IndicatorHistoryMode.Full : IndicatorHistoryMode.LatestOnly).BuildAsync();
            Array.Clear(bars);
            return (run, sma, expected);
        })));
        try
        {
            // Later dispatches reuse the same pool before any retained result is
            // checked. Disposal must not return a published column to that pool.
            foreach (var result in completed) result.run.Dispose();
            for (int i = 0; i < 3; i++)
            {
                using var later = await Builder(Data(100000, 1), new Sma(20))
                    .ConfigureHistory(IndicatorHistoryMode.Full).BuildAsync();
            }
            foreach (var result in completed) Bits(result.expected, result.run[result.sma].ToArray());
        }
        finally { foreach (var result in completed) result.run.Dispose(); }
    }

    [Theory]
    [InlineData(65535, 0)] [InlineData(65536, 0)] [InlineData(65537, 0)]
    [InlineData(65537, 1)] [InlineData(65537, 2)]
    public async Task FullParallelSmaMatchesGuardedValuesAndOwnsReplay(int count, int mode)
    {
        var bars = Data(count, mode);
        var original = bars.ToArray();
        var sma = new Sma(20);
        var expected = new double[count];
        OoplesFinance.StockIndicators.Core.MovingAverageCore.SimpleMovingAverage(
            bars.Select(b => b.Close).ToArray(), expected, 20);
        var run = await Builder(bars, sma).BuildAsync();
        Bits(expected, run[sma].ToArray());
        Array.Clear(bars);
        int index = 0;
        IBarSnapshot? retained = null;
        await foreach (var snapshot in run)
        {
            Assert.Equal(original[index], snapshot.Bar);
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected[index]), BitConverter.DoubleToInt64Bits(snapshot[sma]));
            retained = snapshot;
            index++;
        }
        Assert.Equal(count, index);
        run.Dispose();
        Assert.Equal(original[^1], retained!.Bar);
        Bits(expected, run[sma].ToArray());
    }

    [Theory]
    [InlineData(8191)] [InlineData(8192)] [InlineData(8193)] [InlineData(65537)]
    public async Task FullParallelAsinOwnsReplayAndPreservesDomainBits(int count)
    {
        double[] domain = [-0d, 0d, -1d, 1d, double.Epsilon, -double.Epsilon,
            Math.BitIncrement(1d), Math.BitDecrement(-1d), .17d];
        var bars = Enumerable.Range(0, count).Select(i => new Bar(default, 1, 1, 1, domain[i % domain.Length], 1)).ToArray();
        var original = bars.ToArray();
        var asin = new PriceCircularTransform(PriceCircularOperation.ArcSine);
        using var run = await Builder(bars, asin).BuildAsync();
        Array.Clear(bars);
        int index = 0;
        await foreach (var snapshot in run)
        {
            var close = original[index].Close;
            bool defined = close is >= -1 and <= 1;
            Assert.Equal(original[index], snapshot.Bar);
            Assert.Equal(BitConverter.DoubleToInt64Bits(defined ? Math.Asin(close) : 0),
                BitConverter.DoubleToInt64Bits(snapshot[asin.Value]));
            Assert.Equal(defined ? 1d : 0d, snapshot[asin.IsDefined]);
            index++;
        }
        Assert.Equal(count, index);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task FullParallelPilotsPreserveValidationOrderAndRecover(bool sma)
    {
        var bars = Data(65537);
        var original = bars[8192];
        bars[8192] = new Bar(default, 0, double.NaN, 0, .5, 1);
        var builder = Builder(bars, sma ? new Sma(20) : new PriceCircularTransform(PriceCircularOperation.ArcSine));
        var expected = Record.Exception(() => OoplesFinance.StockIndicators.Validation.IndicatorInputDomain.Finite.Validate(in bars[8192]));
        bars[^1] = new Bar(default, 0, 1, 0, .5, double.NaN);
        var actual = await Record.ExceptionAsync(() => builder.BuildAsync());
        Assert.NotNull(actual);
        Assert.Equal(expected!.GetType(), actual.GetType());
        Assert.Equal(expected.Message, actual.Message);
        Assert.Null(builder.LastExecution);
        bars[8192] = original;
        bars[^1] = original;
        using var recovered = await builder.BuildAsync();
        Assert.Equal(bars[^1], recovered.Latest.Bar);
    }

    [Theory]
    [InlineData(8191)] [InlineData(8192)] [InlineData(8193)]
    [InlineData(65535)] [InlineData(65536)] [InlineData(65537)] [InlineData(100001)]
    public async Task LargeAsinPreservesExactBitsDomainsAndOwnedSnapshot(int count)
    {
        double[] edge = [-0d, 0d, -1d, 1d, double.Epsilon, -double.Epsilon,
            Math.BitIncrement(1d), Math.BitDecrement(-1d), double.MaxValue, -double.MaxValue];
        var close = Enumerable.Range(0, count).Select(i => i % 31 < edge.Length
            ? edge[i % 31] : (i % 2049 - 1024) / 1024d).ToArray();
        var bars = close.Select(x => new Bar(default, x, x, x, x, 1)).ToArray();
        var asin = new PriceCircularTransform(PriceCircularOperation.ArcSine);
        using var run = await Builder(bars, asin).ConfigureHistory(IndicatorHistoryMode.LatestOnly).BuildAsync();
        var last = bars[^1];
        Array.Clear(bars);
        Assert.Equal(last, run.Latest.Bar);
        for (int i = 0; i < count; i++)
        {
            bool defined = close[i] is >= -1 and <= 1;
            double expected = defined ? Math.Asin(close[i]) : 0;
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(run[asin.Value][i]));
            Assert.Equal(defined ? 1d : 0d, run[asin.IsDefined][i]);
        }
    }

    [Fact]
    public async Task ConcurrentLargeAsinBuildsRemainIsolatedAndReleaseDispatchAfterFailure()
    {
        await Task.WhenAll(Enumerable.Range(0, 8).Select(k => Task.Run(async () =>
        {
            var bars = Data(65536 + k);
            var asin = new PriceCircularTransform(PriceCircularOperation.ArcSine);
            var builder = Builder(bars, asin).ConfigureHistory(IndicatorHistoryMode.LatestOnly);
            using var run = await builder.BuildAsync();
            for (int i = 0; i < bars.Length; i++)
                Assert.Equal(BitConverter.DoubleToInt64Bits(Math.Asin(bars[i].Close)), BitConverter.DoubleToInt64Bits(run[asin.Value][i]));
            bars[^1] = new Bar(default, 0, 1, 0, .5, double.NaN);
            await Assert.ThrowsAnyAsync<ArgumentException>(() => builder.BuildAsync());
            Assert.Null(builder.LastExecution);
            bars[^1] = new Bar(default, 0, 1, 0, .5, 1);
            using var recovered = await builder.BuildAsync();
            Assert.Equal(Math.Asin(.5), recovered[asin.Value][bars.Length - 1]);
        })));
    }

    [Theory]
    [InlineData(0, false)] [InlineData(16384, false)] [InlineData(32768, false)] [InlineData(49152, false)]
    [InlineData(0, true)] [InlineData(16384, true)] [InlineData(32768, true)] [InlineData(49152, true)]
    public async Task ParallelPilotsReportFirstInvalidOwnedBarInSourceOrder(int first, bool sma)
    {
        var bars = Data(65537);
        bars[first] = new Bar(default, 0, double.NaN, 0, .5, 1);
        bars[^1] = new Bar(default, 0, 1, 0, .5, double.PositiveInfinity);
        var expected = Record.Exception(() => OoplesFinance.StockIndicators.Validation.IndicatorInputDomain.Finite.Validate(in bars[first]));
        var builder = Builder(bars, sma ? new Sma(20) : new PriceCircularTransform(PriceCircularOperation.ArcSine))
            .ConfigureHistory(IndicatorHistoryMode.LatestOnly);
        var actual = await Record.ExceptionAsync(() => builder.BuildAsync());
        Assert.NotNull(expected);
        Assert.NotNull(actual);
        Assert.Equal(expected.GetType(), actual.GetType());
        Assert.Equal(expected.Message, actual.Message);
        Assert.Null(builder.LastExecution);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task LargeSmaPreservesFullHistoryBitsAcrossAllArithmeticRoutes(int mode)
    {
        var bars = Data(65537, mode);
        foreach (int period in new[] { 1, 20, 1000, int.MaxValue })
        {
            var sma = new Sma(period);
            using var expected = await Builder(bars, sma).BuildAsync();
            using var actual = await Builder(bars, sma).ConfigureHistory(IndicatorHistoryMode.LatestOnly).BuildAsync();
            Bits(expected[sma].ToArray(), actual[sma].ToArray());
            Assert.Equal(expected.Latest.Bar, actual.Latest.Bar);
            Assert.Equal(expected.Latest.IsWarmedUp, actual.Latest.IsWarmedUp);
        }
    }

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
        var values = await builder.ConfigureHistory(IndicatorHistoryMode.LatestOnly).BuildAsync();
        Assert.Equal(history.BarCount, values.BarCount);
        Assert.True(values.IsComplete);
        Assert.Equal(history.Latest.Bar, values.Latest.Bar);
        Assert.Equal(history.Latest.Index, values.Latest.Index);
        Assert.Equal(history.Latest.IsWarmedUp, values.Latest.IsWarmedUp);
        var latest = values.Latest;
        var savedBar = latest.Bar;
        var saved = indicator.Outputs.Select(o => values[o].ToArray()).ToArray();
        for (int slot = 0; slot < saved.Length; slot++) Bits(history[indicator.Outputs[slot]].ToArray(), saved[slot]);
        Array.Clear(bars);
        _ = await builder.ConfigureHistory(IndicatorHistoryMode.LatestOnly).BuildAsync();
        history.Dispose();
        values.Dispose();
        Assert.Equal(savedBar, latest.Bar);
        Assert.Equal(savedBar, values.Latest.Bar);
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
        var actual = await builder.ConfigureHistory(IndicatorHistoryMode.LatestOnly).BuildAsync();
        foreach (var indicator in indicators)
            foreach (var output in indicator.Outputs) Bits(expected[output].ToArray(), actual[output].ToArray());
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task ComposedOwnedColumnsPreserveBitsWarmupAndOwnership(int data)
    {
        foreach (int count in new[] { 0, 1, 19, 2051 })
        foreach (int period in new[] { 1, 20, 4096 })
        foreach (bool publishMean in new[] { false, true })
        {
            var bars = Data(count, data);
            var sma = new Sma(period);
            var asin = new PriceCircularTransform(PriceCircularOperation.ArcSine);
            asin.Of(sma);
            var other = new Sma(7);
            var otherAsin = new PriceCircularTransform(PriceCircularOperation.ArcSine);
            otherAsin.Of(other);
            var direct = new PriceCircularTransform(PriceCircularOperation.ArcSine);
            var indicators = publishMean ? new IIndicator[] { sma, asin, other, otherAsin, direct, asin }
                : new IIndicator[] { asin, otherAsin, direct };
            using var expected = await Builder(bars, indicators).BuildAsync();
            using var actual = await Builder(bars, indicators).ConfigureHistory(IndicatorHistoryMode.LatestOnly).BuildAsync();
            foreach (var output in indicators.SelectMany(i => i.Outputs))
                Bits(expected[output].ToArray(), actual[output].ToArray());
            if (!publishMean) Assert.Throws<KeyNotFoundException>(() => actual[sma].ToArray());
            if (count == 0) Assert.Throws<InvalidOperationException>(() => actual.Latest);
            else
            {
                var last = bars[^1];
                Array.Clear(bars);
                Assert.Equal(last, actual.Latest.Bar);
                Assert.Equal(expected.Latest.IsWarmedUp, actual.Latest.IsWarmedUp);
            }
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ComposedAllocationsExcludeTemporaryHistory(bool publishMean)
    {
        var sma = new Sma(20);
        var asin = new PriceCircularTransform(PriceCircularOperation.ArcSine);
        asin.Of(sma);
        var builder = Builder(Data(10_000), publishMean ? new IIndicator[] { sma, asin } : new IIndicator[] { asin })
            .ConfigureHistory(IndicatorHistoryMode.LatestOnly);
        using var warm = builder.BuildAsync().GetAwaiter().GetResult();
        long before = GC.GetAllocatedBytesForCurrentThread();
        using var run = builder.BuildAsync().GetAwaiter().GetResult();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.InRange(allocated, publishMean ? 240_000 : 160_000, publishMean ? 270_000 : 190_000);
        GC.KeepAlive(run);
    }

    [Fact]
    public async Task ComposedValidationAndCancellationClearDiagnostics()
    {
        var bars = Data(2051);
        var asin = new PriceCircularTransform(PriceCircularOperation.ArcSine);
        asin.Of(new Sma(20));
        var builder = Builder(bars, asin).ConfigureHistory(IndicatorHistoryMode.LatestOnly);
        using var prior = await builder.BuildAsync();
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancel.Token));
        Assert.Null(builder.LastExecution);
        bars[^1] = new Bar(default, 0, 1, 0, .5, double.NaN);
        var expected = await Record.ExceptionAsync(() => Builder(bars, asin).BuildAsync());
        var actual = await Record.ExceptionAsync(() => builder.BuildAsync());
        Assert.NotNull(expected);
        Assert.NotNull(actual);
        Assert.Equal(expected.GetType(), actual.GetType());
        Assert.Equal(expected.Message, actual.Message);
        Assert.Null(builder.LastExecution);
        Assert.Equal(2051, prior.BarCount);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(19)]
    public async Task EmptyAndIncompleteWindows(int count)
    {
        var indicators = new IIndicator[] { new Sma(int.MaxValue), new RetrospectiveFractals(int.MaxValue, 2) };
        var builder = Builder(Data(count), indicators);
        var actual = await builder.ConfigureHistory(IndicatorHistoryMode.LatestOnly).BuildAsync();
        Assert.Equal(count, actual.BarCount);
        if (count == 0) Assert.Throws<InvalidOperationException>(() => actual.Latest);
        else Assert.False(actual.Latest.IsWarmedUp);
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
        var actual = await builder.ConfigureHistory(IndicatorHistoryMode.LatestOnly).BuildAsync();
        Bits(expected[asin.Value].ToArray(), actual[asin.Value].ToArray());
        Bits(expected[asin.IsDefined].ToArray(), actual[asin.IsDefined].ToArray());
        Assert.Equal(65, actual.BarCount);
        Assert.Equal(expected.Latest.Bar, actual.Latest.Bar);
        Assert.Equal(expected.Latest.IsWarmedUp, actual.Latest.IsWarmedUp);
        Assert.Equal(expected.Latest[asin.Value], actual.Latest[asin.Value]);
        Assert.Throws<KeyNotFoundException>(() => actual[sma].ToArray());
    }

    [Fact]
    public async Task InvalidInputAndCancellationCannotPublishResults()
    {
        var bars = Data(40);
        var builder = Builder(bars, new Sma(3));
        _ = await builder.ConfigureHistory(IndicatorHistoryMode.LatestOnly).BuildAsync();
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.ConfigureHistory(IndicatorHistoryMode.LatestOnly).BuildAsync(cancellation.Token));
        Assert.Null(builder.LastExecution);
        bars[^1] = new Bar(default, double.NaN, 1, 0, .5, 1);
        await Assert.ThrowsAnyAsync<ArgumentException>(() => builder.ConfigureHistory(IndicatorHistoryMode.LatestOnly).BuildAsync());
        Assert.Null(builder.LastExecution);

    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RawValidationPrecedesUnrepresentableArithmetic(bool invalidTail)
    {
        var bars = new[] { new Bar(default, 0, double.MaxValue, -double.MaxValue, 0, 1),
            new Bar(default, 0, 1, 0, 0, invalidTail ? double.NaN : 1) };
        var builder = Builder(bars, new ScaledTrueRange(1));
        var expected = await Record.ExceptionAsync(() => Builder(bars, new ScaledTrueRange(1)).BuildAsync());
        var actual = await Record.ExceptionAsync(() => builder.ConfigureHistory(IndicatorHistoryMode.LatestOnly).BuildAsync());
        Assert.NotNull(expected);
        Assert.NotNull(actual);
        Assert.Equal(expected.GetType(), actual.GetType());
        Assert.Null(builder.LastExecution);
    }

    [Theory]
    [InlineData(0, 0)] [InlineData(0, 1)] [InlineData(0, 2)] [InlineData(0, 3)] [InlineData(0, 4)]
    [InlineData(1, 0)] [InlineData(1, 1)] [InlineData(1, 2)] [InlineData(1, 3)] [InlineData(1, 4)]
    public async Task PilotVectorValidationPreservesEveryFieldAndError(int kind, int field)
    {
        foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (int position in new[] { 0, 4, 8 })
        {
            var bars = Data(9);
            var fields = new[] { .25, .5, 0d, .25, 1d };
            fields[field] = invalid;
            bars[position] = new Bar(default, fields[0], fields[1], fields[2], fields[3], fields[4]);
            var indicator = Indicator(kind);
            var builder = Builder(bars, indicator).ConfigureHistory(IndicatorHistoryMode.LatestOnly);
            var expected = await Record.ExceptionAsync(() => Builder(bars, indicator).BuildAsync());
            var actual = await Record.ExceptionAsync(() => builder.BuildAsync());
            Assert.NotNull(expected);
            Assert.NotNull(actual);
            Assert.Equal(expected.GetType(), actual.GetType());
            Assert.Equal(expected.Message, actual.Message);
            Assert.Null(builder.LastExecution);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PilotAllocationsExcludeFullBarHistory(bool sma)
    {
        var builder = Builder(Data(10_000), sma ? new Sma(20) : new PriceCircularTransform(PriceCircularOperation.ArcSine));
        _ = builder.ConfigureHistory(IndicatorHistoryMode.LatestOnly).BuildAsync().GetAwaiter().GetResult();
        long before = GC.GetAllocatedBytesForCurrentThread();
        var result = builder.ConfigureHistory(IndicatorHistoryMode.LatestOnly).BuildAsync().GetAwaiter().GetResult();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        // SMA owns its output plus a period-sized buffer. Asin owns values and flags.
        Assert.InRange(allocated, sma ? 80_000 : 160_000, sma ? 100_000 : 200_000);
        GC.KeepAlive(result);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task HistoryConfigurationPreservesFullReplayAndRejectsLatestOnlyReplay(bool projected)
    {
        var bars = Data(7);
        var sma = new Sma(3);
        var builder = new StockIndicatorBuilder().ConfigureIndicators(sma)
            .ConfigureSource(projected ? Bars.From(bars, b => b) : Bars.From(bars));
        using var full = await builder.BuildAsync();
        var replay = new List<Bar>();
        await foreach (var snapshot in full) replay.Add(snapshot.Bar);
        Assert.Equal(bars, replay);
        using var latest = await builder.ConfigureHistory(IndicatorHistoryMode.LatestOnly).BuildAsync();
        var error = Assert.Throws<InvalidOperationException>(() => latest.GetAsyncEnumerator());
        Assert.Contains("ConfigureHistory", error.Message);
        using var restored = await builder.ConfigureHistory(IndicatorHistoryMode.Full).BuildAsync();
        int count = 0;
        await foreach (var snapshot in restored) count++;
        Assert.Equal(bars.Length, count);
    }

    [Fact]
    public async Task LatestOnlyLiveFeedsContinueToEnumerateNewSnapshots()
    {
        var feed = Bars.Live();
        var asin = new PriceCircularTransform(PriceCircularOperation.ArcSine);
        using var run = await new StockIndicatorBuilder().ConfigureSource(feed).ConfigureIndicators(asin)
            .ConfigureHistory(IndicatorHistoryMode.LatestOnly).BuildAsync();
        Assert.False(run.IsComplete);
        var bars = Data(7);
        foreach (var bar in bars) feed.Publish(bar);
        feed.Complete();
        int count = 0;
        await foreach (var snapshot in run)
        {
            Assert.Equal(bars[count], snapshot.Bar);
            Assert.Equal(Math.Asin(bars[count].Close), snapshot[asin.Value]);
            count++;
        }
        Assert.Equal(bars.Length, count);
        Assert.Equal(bars[^1], run.Latest.Bar);
    }

    [Fact]
    public void FacadeHasOneAsynchronousBuildEndpointAndValidatedHistoryConfiguration()
    {
        Assert.Null(typeof(StockIndicatorBuilder).GetMethod("BuildValuesAsync"));
        Assert.Null(typeof(IIndicatorRun).Assembly.GetType("OoplesFinance.StockIndicators.Indicators.IIndicatorValues"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StockIndicatorBuilder().ConfigureHistory((IndicatorHistoryMode)99));
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(20)] [InlineData(64)]
    public async Task LatestWarmupMatchesFullHistoryAtTheBoundary(int count)
    {
        var sma = new Sma(20);
        using var full = await Builder(Data(count), sma).BuildAsync();
        using var latest = await Builder(Data(count), sma).ConfigureHistory(IndicatorHistoryMode.LatestOnly).BuildAsync();
        Assert.Equal(full.Latest.IsWarmedUp, latest.Latest.IsWarmedUp);
        Assert.Equal(full.Latest[sma], latest.Latest[sma]);
    }

    [Fact]
    public async Task UncertifiedSmaPreservesExactFallbackForOverflowingSums()
    {
        var bars = Enumerable.Repeat(new Bar(default, 0, 0, 0, double.MaxValue, 1), 7).ToArray();
        var sma = new Sma(2);
        using var full = await Builder(bars, sma).BuildAsync();
        using var latest = await Builder(bars, sma).ConfigureHistory(IndicatorHistoryMode.LatestOnly).BuildAsync();
        Bits(full[sma].ToArray(), latest[sma].ToArray());
        Assert.All(latest[sma].ToArray(), value => Assert.True(double.IsFinite(value)));
        Assert.Equal(double.MaxValue, latest.Latest[sma]);
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
        var values = await builder.ConfigureHistory(IndicatorHistoryMode.LatestOnly).BuildAsync();
        Assert.Equal(IndicatorExecutionBackend.Gpu, builder.LastExecution!.Backend);
        using var expected = await Builder(bars, sma).BuildAsync();
        Array.Clear(bars);
        Bits(expected[sma].ToArray(), values[sma].ToArray());
    }
}

using System.Runtime.CompilerServices;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class BuilderStorageTests
{
    private static Bar Candle(int i) => new(DateTime.UnixEpoch.AddMinutes(i), i + 1, i + 3, i, i + 2, 100);

    [Fact]
    public async Task SynchronousAndAsynchronousSourcesPublishIdenticalOwnedHistory()
    {
        var bars = Enumerable.Range(0, 80).Select(Candle).ToArray();
        var average = new Sma(7);
        var transform = new PriceCircularTransform(PriceCircularOperation.ArcTangent);
        using var direct = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(average, transform).BuildAsync();
        using var asynchronous = await new StockIndicatorBuilder().ConfigureSource(new AsyncSource(bars))
            .ConfigureIndicators(average, transform).BuildAsync();
        Assert.Equal(direct[average].ToArray(), asynchronous[average].ToArray());
        Assert.Equal(direct[transform.Value].ToArray(), asynchronous[transform.Value].ToArray());
        var expectedLast = bars[^1];
        bars[^1] = Candle(1000);
        Assert.Equal(expectedLast, direct.Latest.Bar);
        Assert.Equal(expectedLast, asynchronous.Latest.Bar);
        using var later = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(average).BuildAsync();
        Assert.NotEqual(direct[average][^1], later[average][^1]);
        later.Dispose();
        Assert.Equal(direct[average].ToArray(), asynchronous[average].ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SingleUseProjectionIsDisposedAtFirstInvalidOrCancelledBar(bool cancel)
    {
        var enumerations = 0;
        var projected = 0;
        var disposed = false;
        using var cancellation = new CancellationTokenSource();
        IEnumerable<int> Items()
        {
            if (++enumerations != 1) throw new InvalidOperationException("Read twice");
            try { for (var i = 0; i < 10; i++) yield return i; }
            finally { disposed = true; }
        }
        var source = Bars.From(Items(), i =>
        {
            projected++;
            if (i == 2 && cancel) cancellation.Cancel();
            return i == 2 && !cancel ? new Bar(default, double.NaN, 3, 0, 2, 0) : Candle(i);
        });
        var build = new StockIndicatorBuilder().ConfigureSource(source).ConfigureIndicators(new Sma(2));
        if (cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => build.BuildAsync(cancellation.Token));
        else await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => build.BuildAsync());
        Assert.Equal(1, enumerations);
        Assert.Equal(3, projected);
        Assert.True(disposed);
    }

    [Fact]
    public async Task WarmupPrimesStateButIsNotPublished()
    {
        var average = new Sma(3);
        var bars = Enumerable.Range(0, 6).Select(Candle).ToArray();
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars.Skip(3)).WarmedWith(bars.Take(3)))
            .ConfigureIndicators(average).BuildAsync();
        Assert.Equal(new[] { 4d, 5d, 6d }, run[average].ToArray());
        var snapshots = new List<IBarSnapshot>();
        await foreach (var snapshot in run) snapshots.Add(snapshot);
        Assert.Equal(bars.Skip(3), snapshots.Select(snapshot => snapshot.Bar));
        Assert.All(snapshots, snapshot => Assert.True(snapshot.IsWarmedUp));
    }

    [Fact]
    public void FreshBuilderHasBoundedOwnedStorageForTenThousandBars()
    {
        var bars = Enumerable.Range(0, 10_000).Select(Candle).ToArray();
        static IIndicatorRun Build(Bar[] input) => new StockIndicatorBuilder().ConfigureSource(Bars.From(input))
            .ConfigureIndicators(new PriceCircularTransform(PriceCircularOperation.ArcTangent)).BuildAsync().GetAwaiter().GetResult();
        using (Build(bars)) { }
        var before = GC.GetAllocatedBytesForCurrentThread();
        using var run = Build(bars);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(bars.Length, run.BarCount);
        // Custom-only runs own history and two outputs, without legacy OHLCV columns.
        // The former eager bridge alone added another six full-size columns.
        Assert.True(allocated < 800_000, $"Allocated {allocated:N0} bytes for one fresh builder.");
    }

    [Theory]
    [InlineData(0, "Open")]
    [InlineData(1, "High")]
    [InlineData(2, "Low")]
    [InlineData(3, "Close")]
    [InlineData(4, "Volume")]
    public void FiniteFastPathPreservesFieldDiagnostics(int field, string name)
    {
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var fields = new[] { double.Epsilon, -double.MaxValue, double.MaxValue, -0d, 0d };
            fields[field] = invalid;
            var bar = new Bar(default, fields[0], fields[1], fields[2], fields[3], fields[4]);
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorInputDomain.Finite.Validate(bar));
            Assert.StartsWith(name + " must be finite.", exception.Message);
        }
        IndicatorInputDomain.Finite.Validate(new Bar(default, double.Epsilon, -double.MaxValue, double.MaxValue, -0d, 0));
    }

    [Fact]
    public void EmptyLegacyGraphStillPublishesWithoutMaterializingValidatedHistory()
    {
        var deferred = new Lazy<StockData>(() => throw new InvalidOperationException("Unnecessary column materialization"));
        using var runtime = new StockIndicatorBuilder(IndicatorDataSource.FromValidatedHistory(deferred)).Build();
        var publications = 0;
        runtime.Updated += _ => publications++;
        runtime.Start();
        runtime.Start();
        Assert.Equal(1, publications);
        Assert.NotNull(runtime.Latest);
        Assert.False(deferred.IsValueCreated);
    }

    [Fact]
    public void UnknownLegacySubscriptionPreservesTheOrdinaryEvaluatorError()
    {
        static StockData Data() => new(new[] { 1d }, new[] { 2d }, new[] { 0d }, new[] { 1d }, new[] { 0d }, new[] { DateTime.UnixEpoch });
        using var ordinary = new StockIndicatorBuilder(IndicatorDataSource.FromBatch(Data())).Build();
        var deferred = new Lazy<StockData>(Data);
        using var optimized = new StockIndicatorBuilder(IndicatorDataSource.FromValidatedHistory(deferred)).Build();
        ordinary.Subscribe(default(SeriesHandle));
        optimized.Subscribe(default(SeriesHandle));
        var expected = Record.Exception(() => ordinary.Start());
        var actual = Record.Exception(() => optimized.Start());
        Assert.NotNull(expected);
        Assert.NotNull(actual);
        Assert.Equal(expected.GetType(), actual.GetType());
        Assert.Equal(expected.Message, actual.Message);
        Assert.True(deferred.IsValueCreated);
    }

    [Fact]
    public async Task DeferredHistoryRemainsAvailableForLaterLegacyEvaluation()
    {
        var bars = Enumerable.Range(0, 6).Select(Candle).ToArray();
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(new ScaledTrueRange());
        using var typed = await builder.BuildAsync();
        bars[0] = Candle(1000);
        SeriesHandle average = default;
        using var legacy = builder.ConfigureIndicators(catalog => average = catalog.Sma(3)).Build();
        legacy.Start();
        Assert.Equal(new[] { 0d, 0d, 3d, 4d, 5d, 6d }, legacy.GetSeries(average).ToArray());
    }

    [Fact]
    public void MaterializedDeferredDataAndNamedSourcesStillReceiveFullValidation()
    {
        static StockData Data() => new(new[] { 1d }, new[] { 2d }, new[] { 0d }, new[] { 1d }, new[] { 0d }, new[] { DateTime.UnixEpoch });
        var source = IndicatorDataSource.FromValidatedHistory(new Lazy<StockData>(Data));
        source.BatchData!.OpenPrices[0] = double.NaN;
        using var changed = new StockIndicatorBuilder(source).Build();
        Assert.Throws<ArgumentOutOfRangeException>(() => changed.Start());
        var named = Data();
        named.ClosePrices[0] = double.NaN;
        var untouched = IndicatorDataSource.FromValidatedHistory(new Lazy<StockData>(Data));
        using var withNamed = new StockIndicatorBuilder(untouched)
            .AddDataSource("invalid", IndicatorDataSource.FromBatch(named)).Build();
        Assert.Throws<ArgumentOutOfRangeException>(() => withNamed.Start());
    }

    [Fact]
    public async Task CustomerDomainGettersKeepTheirValidationCalls()
    {
        var indicator = new DomainProbe(IndicatorInputDomain.Finite);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(Enumerable.Range(0, 5).Select(Candle)))
            .ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(15, indicator.Reads); // Eager graph validation, engine entry, then each update.
        Assert.Equal(Enumerable.Range(0, 5).Select(i => (double)i + 2), run[indicator].ToArray());
    }

    [Fact]
    public async Task ChainedInputStillChecksTheDerivedCloseBeforeConsumingIt()
    {
        var indicator = new DomainProbe(IndicatorInputDomain.PositiveClose);
        indicator.Of(new NegativeClose());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(new[] { Candle(0) })).ConfigureIndicators(indicator).BuildAsync());
        Assert.Equal(0, indicator.Updates);
    }

    [Fact]
    public void EngineWithoutValidationProofRejectsInvalidRawBars()
    {
        var engine = new CustomIndicatorEngine(new[] { new Bar(default, double.NaN, 2, 0, 1, 0) }, _ => null);
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.Compute(new ScaledTrueRange()));
    }

    private sealed class DomainProbe(IndicatorInputDomain domain) : IndicatorBase, IIndicatorInputDomainContract
    {
        public int Reads { get; private set; }
        public int Updates { get; private set; }
        public IndicatorInputDomain InputDomain { get { Reads++; return domain; } }
        protected internal override object CreateState() => new State(this);
        private sealed class State(DomainProbe owner) : IIndicatorState
        {
            public void Reset() => owner.Updates = 0;
            public double Update(in Bar bar) { owner.Updates++; return bar.Close; }
        }
    }

    private sealed class NegativeClose : IndicatorBase
    {
        protected internal override object CreateState() => new State();
        private sealed class State : IIndicatorState
        {
            public void Reset() { }
            public double Update(in Bar bar) => -Math.Abs(bar.Close);
        }
    }

    private sealed class AsyncSource(Bar[] bars) : IBarSource
    {
        public bool IsFinite => true;
        public async IAsyncEnumerable<Bar> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var bar in bars)
            {
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
                yield return bar;
            }
        }
        public async IAsyncEnumerable<Bar> ReadWarmupAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}

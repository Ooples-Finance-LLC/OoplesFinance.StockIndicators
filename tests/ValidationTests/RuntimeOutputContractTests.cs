using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class RuntimeOutputContractTests
{
    private static Bar At(int index, double value = 1, double volume = 1) =>
        new(DateTime.UnixEpoch.AddMinutes(index), value, value, value, value, volume);

    [Fact]
    public Task SharedValidationChecksCustomerPrimaryOutputsAutomatically() =>
        IndicatorValidation.ValidateAndThrowAsync(new(typeof(NamedPrimary), "named-primary", () => new NamedPrimary(),
            IndicatorValidationRule.Reference(0, bars => bars.Select(b => b.Close).ToArray(), IndicatorErrorBudget.Exact),
            IndicatorValidationRule.Reference(1, bars => bars.Select(b => -b.Close).ToArray(), IndicatorErrorBudget.Exact)));

    [Fact]
    public async Task HistoricalSnapshotsIncludeDependencyWarmupAndPrimingHistory()
    {
        var child = new CountingConsumer();
        child.Of(new Sma(3));
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(new[] { At(0), At(1), At(2) }))
            .ConfigureIndicators(child).BuildAsync();
        var readiness = new List<bool>();
        await foreach (var snapshot in run) readiness.Add(snapshot.IsWarmedUp);
        Assert.Equal(new[] { false, false, true }, readiness);
        using var shortRun = await new StockIndicatorBuilder().ConfigureSource(Bars.From(new[] { At(0) }))
            .ConfigureIndicators(new Sma(3)).BuildAsync();
        Assert.False(shortRun.Latest.IsWarmedUp);
        using var primed = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(new[] { At(2) }).WarmedWith(new[] { At(0), At(1) }))
            .ConfigureIndicators(new Sma(3)).BuildAsync();
        Assert.True(primed.Latest.IsWarmedUp);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LiveStartupNaNsNeverMarkSnapshotsReady(bool publishEarly)
    {
        var source = Bars.Live();
        var indicator = new Unavailable(false);
        var builder = new StockIndicatorBuilder().ConfigureSource(source).ConfigureIndicators(indicator);
        if (publishEarly) builder.PublishBeforeWarmup();
        using var run = await builder.BuildAsync();
        foreach (var index in new[] { 0, 1, 2 }) source.Publish(At(index));
        source.Complete();
        var readiness = new List<bool>();
        await foreach (var snapshot in run)
        {
            readiness.Add(snapshot.IsWarmedUp);
            Assert.Equal(snapshot.IsWarmedUp, run.Latest.IsWarmedUp);
            Assert.Equal(!double.IsNaN(snapshot[indicator]), snapshot.IsWarmedUp);
        }
        Assert.Equal(publishEarly ? new[] { false, false, true } : new[] { true }, readiness);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SnapshotsUseTheSameDeclaredPrimaryOutputAsTheRun(bool live)
    {
        var feed = Bars.Live();
        var indicator = new NamedPrimary();
        var chained = new CountingConsumer();
        chained.Of(indicator);
        var composed = new ComponentConsumer(indicator);
        IBarSource source = live ? feed : Bars.From(new[] { At(0, 3), At(1, 7) });
        using var run = await new StockIndicatorBuilder().ConfigureSource(source).ConfigureIndicators(indicator, chained, composed).BuildAsync();
        if (live) { feed.Publish(At(0, 3)); feed.Publish(At(1, 7)); feed.Complete(); }
        await foreach (var snapshot in run)
        {
            Assert.Equal(-snapshot.Bar.Close, snapshot[indicator]);
            Assert.Equal(snapshot[indicator.PrimaryOutput], snapshot[indicator]);
            Assert.Equal(snapshot[indicator], snapshot[chained]);
            Assert.Equal(2 * snapshot[indicator], snapshot[composed]);
        }
        Assert.Equal(-7, run.Latest[indicator]);
        Assert.Equal(run[indicator].ToArray().Last(), run.Latest[indicator]);
    }

    [Fact]
    public async Task RuntimeWarmupNeverPermitsInfinity()
    {
        var error = await Assert.ThrowsAsync<IndicatorOutputException>(() => new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(new[] { At(0) })).ConfigureIndicators(new CorrectnessAssuranceTests.Probe(1)).BuildAsync());
        Assert.Equal(0, error.BarIndex);
        Assert.True(double.IsPositiveInfinity(error.Value));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedFiniteOrLiveWarmupCalculationDisposesItsCustomState(bool liveWarmup)
    {
        var indicator = new DisposableFailure();
        var bars = new[] { At(0) };
        IBarSource source = liveWarmup ? Bars.Live().WarmedWith(bars) : Bars.From(bars);
        await Assert.ThrowsAsync<IndicatorOutputException>(() => new StockIndicatorBuilder()
            .ConfigureSource(source).ConfigureIndicators(indicator).BuildAsync());
        Assert.True(indicator.StatesCreated > 0);
        Assert.Equal(indicator.StatesCreated, indicator.StatesDisposed);
    }

    [Fact]
    public async Task FiniteBuiltInOverflowCannotPublishAnInfiniteResult()
    {
        var error = await Assert.ThrowsAsync<IndicatorOutputException>(() => new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(new[] { At(0),
                new Bar(DateTime.UnixEpoch.AddMinutes(1), 0, double.MaxValue, -double.MaxValue, 0, 1) }))
            .ConfigureIndicators(new OoplesFinance.StockIndicators.Indicators.Range(1)).BuildAsync());
        Assert.Equal(0, error.OutputSlot);
        Assert.Equal(1, error.BarIndex);
        Assert.Equal(IndicatorStartupPolicy.Finite, error.Policy);
        Assert.Equal(double.PositiveInfinity, error.Value);
    }

    [Fact]
    public async Task ExactCumulativeOverflowIsRejectedByThePrimaryOutputGuard()
    {
        var error = await Assert.ThrowsAsync<IndicatorOutputException>(() => new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(new[] { At(0, 1, double.MaxValue), At(1, 2, double.MaxValue) }))
            .ConfigureIndicators(new OnBalanceVolume(1)).BuildAsync());
        Assert.Equal(typeof(OnBalanceVolume), error.IndicatorType);
        Assert.Equal(0, error.OutputSlot);
        Assert.Equal(1, error.BarIndex);
        Assert.Equal(IndicatorStartupPolicy.Finite, error.Policy);
        Assert.Equal(double.PositiveInfinity, error.Value);
    }

    [Fact]
    public async Task NonfiniteSecondaryOutputFaultsLiveRunWithoutPublishingPartialHistory()
    {
        var source = Bars.Live();
        var indicator = new SharedIndicatorValidationTests.BrokenSecondary();
        using var run = await new StockIndicatorBuilder().ConfigureSource(source).ConfigureIndicators(indicator).BuildAsync();
        source.Publish(At(0));
        source.Complete(); // A removed guard must fail an assertion, not wait forever for another event.
        await using (var read = run.GetAsyncEnumerator())
        {
            var error = await Assert.ThrowsAsync<IndicatorOutputException>(async () => await read.MoveNextAsync());
            Assert.Equal(1, error.OutputSlot);
            Assert.Equal(0, error.BarIndex);
        }
        Assert.Equal(0, run.BarCount);
        Assert.All(indicator.Outputs, slot => Assert.Empty(run[slot].ToArray()));
        await using var retry = run.GetAsyncEnumerator();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await retry.MoveNextAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeclaredUnavailableStartupIsAllowedButStopsAtTheWarmupBoundary(bool staysUnavailable)
    {
        var indicator = new Unavailable(staysUnavailable);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(new[] { At(0), At(1), At(2) }))
            .ConfigureIndicators(indicator);
        if (staysUnavailable)
        {
            var error = await Assert.ThrowsAsync<IndicatorOutputException>(() => builder.BuildAsync());
            Assert.Equal(2, error.BarIndex);
            Assert.Equal(IndicatorStartupPolicy.Finite, error.Policy);
        }
        else
        {
            using var run = await builder.BuildAsync();
            var values = run[indicator].ToArray();
            Assert.True(double.IsNaN(values[0])); Assert.True(double.IsNaN(values[1])); Assert.Equal(1, values[2]);
            await foreach (var snapshot in run) Assert.Equal(snapshot.Index >= 2, snapshot.IsWarmedUp);
        }
    }

    [Fact]
    public async Task InvalidComponentOutputIsRejectedBeforeItsConsumerRuns()
    {
        var invalid = new SharedIndicatorValidationTests.BrokenSecondary();
        var consumer = new CountingConsumer();
        consumer.Of(invalid);
        await Assert.ThrowsAsync<IndicatorOutputException>(() => new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(new[] { At(0) })).ConfigureIndicators(consumer).BuildAsync());
        Assert.Equal(0, consumer.Updates);
    }

    private sealed class Unavailable(bool staysUnavailable) : IndicatorBase, IIndicatorStartupContract
    {
        public override int WarmupBars => 2;
        public IndicatorStartupPolicy StartupPolicy(int outputSlot) => IndicatorStartupPolicy.NaN;
        protected internal override object CreateState() => new State(staysUnavailable);
        private sealed class State(bool staysUnavailable) : IIndicatorState
        {
            private int _count;
            public double Update(in Bar bar) => _count++ < 2 || staysUnavailable ? double.NaN : bar.Close;
            public void Reset() => _count = 0;
        }
    }

    private sealed class CountingConsumer : IndicatorBase
    {
        public int Updates { get; private set; }
        protected internal override object CreateState() => new State(this);
        private sealed class State(CountingConsumer owner) : IIndicatorState
        {
            public double Update(in Bar bar) { owner.Updates++; return bar.Close; }
            public void Reset() => owner.Updates = 0;
        }
    }

    private sealed class DisposableFailure : IndicatorBase
    {
        public int StatesCreated { get; private set; }
        public int StatesDisposed { get; private set; }
        protected internal override object CreateState() { StatesCreated++; return new State(this); }
        private sealed class State(DisposableFailure owner) : IIndicatorState, IDisposable
        {
            public double Update(in Bar bar) => double.PositiveInfinity;
            public void Reset() { }
            public void Dispose() => owner.StatesDisposed++;
        }
    }

    private sealed class NamedPrimary : MultiOutputIndicatorBase, IPrimaryOutputIndicator
    {
        public NamedPrimary() : base(2) { }
        public IIndicatorOutput PrimaryOutput => Outputs[1];
        protected internal override object CreateState() => new State();
        private sealed class State : IMultiOutputState
        {
            public void Update(in Bar bar, Span<double> outputs) { outputs[0] = bar.Close; outputs[1] = -bar.Close; }
            public void Reset() { }
        }
    }

    private sealed class ComponentConsumer : IndicatorBase
    {
        public ComponentConsumer(IIndicator input) => Uses(input);
        protected internal override object CreateState() => new State();
        private sealed class State : IComposedIndicatorState
        {
            public double Update(in Bar bar, ReadOnlySpan<double> components) => 2 * components[0];
            public void Reset() { }
        }
    }
}

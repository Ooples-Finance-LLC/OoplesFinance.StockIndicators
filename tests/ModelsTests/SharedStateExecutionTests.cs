using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedStateExecutionTests
{
    public static IEnumerable<object[]> Cases =>
        from family in Enumerable.Range(0, 5)
        from count in new[] { 0, 1, 41 }
        from composed in new[] { false, true }
        select new object[] { family, count, composed };

    private static IndicatorBase Create(int family) => family switch
    {
        0 => new FirstValueEma(7),
        1 => new RollingPriceSum(7),
        2 => new NormalizedConvolution(new[] { 1d, 2d, 3d }),
        3 => new WindowLinearRegression(7),
        _ => new WindowDispersion(7)
    };

    private static Bar[] Input(int count) => Enumerable.Range(0, count)
        .Select(i => new Bar(default, i + 2, i + 3, i, (i % 9 - 4) / 8d, 1)).ToArray();

    [Theory, MemberData(nameof(Cases))]
    public async Task ReviewedGraphsMatchLegacyBitsAndRetainOwnedSnapshots(int family, int count, bool composed)
    {
        var bars = Input(count);
        var indicator = Create(family);
        if (composed) indicator.Of(new FirstValueEma(3));
        StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator);
        using var reference = await Builder().ConfigureBehavior(_ => { }).BuildAsync();
        var builder = Builder();
        var direct = await builder.BuildAsync();
        using var latest = await Builder().ConfigureHistory(IndicatorHistoryMode.LatestOnly).BuildAsync();
        Assert.True(((IndicatorRun)reference).HasLegacyRuntime);
        Assert.False(((IndicatorRun)direct).HasLegacyRuntime);
        foreach (var output in indicator.Outputs)
        {
            AssertBits(reference[output].ToArray(), direct[output].ToArray());
            AssertBits(reference[output].ToArray(), latest[output].ToArray());
        }
        direct.Dispose();
        Array.Clear(bars);
        foreach (var output in indicator.Outputs)
            AssertBits(reference[output].ToArray(), direct[output].ToArray());
        if (count != 0) Assert.Equal(Input(count)[count - 1], direct.Latest.Bar);
        // The lazy legacy bridge must use the owned bars, even after disposal.
        SeriesHandle price = default;
        using var legacy = builder.ConfigureIndicators(catalog => price = catalog.Price()).Build();
        legacy.Start();
        AssertBits(Input(count).Select(b => b.Close).ToArray(), legacy.GetSeries(price).ToArray());
    }

    [Fact]
    public async Task EveryLegacyConfigurationKeepsRuntime()
    {
        var configurations = new Action<StockIndicatorBuilder>[]
        {
            b => b.ConfigureBehavior(_ => { }), b => b.ConfigureNotifications(), b => b.ConfigureAutoTrading(),
            b => b.ConfigureSignals(), b => b.ConfigureIndicators(new Builder.IndicatorOptions()),
            b => b.ConfigureData(new DataOptions()), b => b.ConfigureSymbols(new SymbolOptions()),
            b => b.ConfigureBacktesting(), b => b.ConfigureBenchmarking()
        };
        foreach (var configure in configurations)
        {
            var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(Input(41)))
                .ConfigureIndicators(new RollingPriceSum(7));
            configure(builder);
            using var run = await builder.BuildAsync();
            Assert.True(((IndicatorRun)run).HasLegacyRuntime);
        }
    }

    [Fact]
    public async Task UnreviewedDependenciesAndCallbackIndicatorsKeepRuntime()
    {
        var calls = 0;
        var callback = new VariablePeriodClassicAverage(_ => { calls++; return 3; }, 2, 3);
        var external = new ExternalIndicator();
        foreach (var source in new IIndicator[] { callback, external, new Ema(3) })
        {
            var root = new RollingPriceSum(7);
            root.Of(source);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(Input(41)))
                .ConfigureIndicators(root).BuildAsync();
            Assert.True(((IndicatorRun)run).HasLegacyRuntime);
        }
        Assert.True(calls > 0);
    }

    [Fact]
    public async Task CancellationInvalidInputAndRequiredGpuAreRejected()
    {
        StockIndicatorBuilder Builder(Bar[] bars) => new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars)).ConfigureIndicators(new RollingPriceSum(7));
        var builder = Builder(Input(41));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
        Assert.Null(builder.LastExecution);
        var invalid = Input(41);
        invalid[3] = new Bar(default, double.NaN, 1, 1, 1, 1);
        var error = await Record.ExceptionAsync(() => Builder(invalid).BuildAsync());
        var legacyError = await Record.ExceptionAsync(() => Builder(invalid).ConfigureBehavior(_ => { }).BuildAsync());
        Assert.NotNull(error);
        Assert.Equal(legacyError!.GetType(), error.GetType());
        Assert.Equal(legacyError.Message, error.Message);
    }

    private static void AssertBits(double[] expected, double[] actual) => Assert.Equal(
        expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));

    private sealed class ExternalIndicator : IndicatorBase
    {
        protected internal override object CreateState() => new State();
        private sealed class State : IIndicatorState
        {
            public double Update(in Bar bar) => bar.Close;
            public void Reset() { }
        }
    }
}

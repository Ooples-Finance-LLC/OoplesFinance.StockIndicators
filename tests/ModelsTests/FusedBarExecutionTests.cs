using System.Runtime.InteropServices;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class FusedBarExecutionTests
{
    [Theory]
    [InlineData(20, false)]
    [InlineData(20, true)]
    [InlineData(1, false)]
    [InlineData(int.MaxValue, false)]
    public async Task SmaDependencyFeedsAsinInTheFusedLoopAndReplaysAfterRejection(int period, bool rejectCertificate)
    {
        var values = Enumerable.Range(0, 2051).Select(i => (i % 13 - 6) / 8d).ToArray();
        if (rejectCertificate) values[2048] = double.Epsilon;
        var sma = new Sma(period);
        var asin = new PriceCircularTransform(PriceCircularOperation.ArcSine);
        asin.Of(sma);
        var bars = BarsFor(values);
        var plan = FusedBarExecution.TryCreate(new IIndicator[] { asin, sma }, bars.Length)!;
        Assert.NotNull(plan);
        plan.Execute(bars, new OwnedBarHistory(), default);
        Assert.Equal(rejectCertificate, plan.UsedSmaFallback);
        var expected = new double[values.Length];
        MovingAverageCore.SimpleMovingAverage(values, expected, period);
        AssertBits(expected, plan.SmaValues!);
        AssertBits(expected.Select(Math.Asin).ToArray(), plan.AsinValues![0]);
        using var fused = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(asin, sma).BuildAsync();
        using var ordinary = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars, bar => bar))
            .ConfigureIndicators(asin, sma).BuildAsync();
        Assert.False(((IndicatorRun)fused).HasLegacyRuntime);
        AssertBits(ordinary[asin.Value].ToArray(), fused[asin.Value].ToArray());
        AssertBits(ordinary[asin.IsDefined].ToArray(), fused[asin.IsDefined].ToArray());
        var dependencyPlan = FusedBarExecution.TryCreate(new[] { asin }, bars.Length)!;
        Assert.Null(dependencyPlan.SmaValues);
        dependencyPlan.Execute(bars, new OwnedBarHistory(), default);
        AssertBits(plan.AsinValues[0], dependencyPlan.AsinValues![0]);
        using var dependencyOnly = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(asin).BuildAsync();
        using var ordinaryDependencyOnly = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars, bar => bar))
            .ConfigureIndicators(asin).BuildAsync();
        Assert.Throws<KeyNotFoundException>(() => dependencyOnly[sma].ToArray());
        Assert.Throws<KeyNotFoundException>(() => ordinaryDependencyOnly[sma].ToArray());
        AssertBits(fused[asin.Value].ToArray(), dependencyOnly[asin.Value].ToArray());
        var fusedWarmup = new List<bool>();
        var ordinaryWarmup = new List<bool>();
        await foreach (var snapshot in dependencyOnly) fusedWarmup.Add(snapshot.IsWarmedUp);
        await foreach (var snapshot in ordinaryDependencyOnly) ordinaryWarmup.Add(snapshot.IsWarmedUp);
        Assert.Equal(ordinaryWarmup, fusedWarmup);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MultiplePilotRegionsShareOwnedInputAndPreserveDistinctOutputs(bool reject)
    {
        var direct = new PriceCircularTransform(PriceCircularOperation.ArcSine);
        var composed = new PriceCircularTransform(PriceCircularOperation.ArcSine);
        var sma = new Sma(20);
        var second = new Sma(50);
        composed.Of(sma);
        var values = Enumerable.Range(0, 2051).Select(i => (i % 19 - 9) / 16d).ToArray();
        if (reject) values[^1] = double.Epsilon;
        var bars = BarsFor(values);
        IIndicator[] indicators = [direct, composed, second];
        using var fused = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicators).BuildAsync();
        using var ordinary = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars, bar => bar))
            .ConfigureIndicators(indicators).BuildAsync();
        Assert.False(((IndicatorRun)fused).HasLegacyRuntime);
        foreach (var indicator in indicators)
            foreach (var output in indicator.Outputs) AssertBits(ordinary[output].ToArray(), fused[output].ToArray());
        Assert.Throws<KeyNotFoundException>(() => fused[sma].ToArray());
        var saved = fused[second].ToArray();
        Array.Clear(bars);
        AssertBits(saved, fused[second].ToArray());
    }

    private static Bar[] BarsFor(double[] values) => values.Select((v, i) =>
        new Bar(DateTime.UnixEpoch.AddMinutes(i), double.MaxValue, double.Epsilon, -double.MaxValue, v, -0d)).ToArray();

    [Theory]
    [InlineData(2, -512)]
    [InlineData(3, -20)]
    [InlineData(20, 0)]
    [InlineData(31, 100)]
    [InlineData(32, 480)]
    public void FusedSmaMatchesIndependentExactWindowRounding(int period, int exponent)
    {
        var random = new Random(483);
        var values = Enumerable.Range(0, 2051).Select(_ => random.Next(-1_000_000, 1_000_000) * Math.Pow(2, exponent)).ToArray();
        values[1024] = -values[1023];
        var plan = FusedBarExecution.TryCreate(new[] { new Sma(period) }, values.Length)!;
        Assert.NotNull(plan);
        plan.Execute(BarsFor(values), new OwnedBarHistory(), default);
        Assert.False(plan.UsedSmaFallback);
        for (var i = 0; i < values.Length; i++)
        {
            var sum = new ReferenceFraction(0);
            if (i >= period - 1)
                for (var j = i - period + 1; j <= i; j++) sum += ReferenceFraction.FromDouble(values[j]);
            Assert.Equal(BitConverter.DoubleToInt64Bits((sum / new ReferenceFraction(period)).ToDouble()),
                BitConverter.DoubleToInt64Bits(plan.SmaValues![i]));
        }
    }

    [Fact]
    public void LateCertificateFailureReplacesEverySpeculativeOutput()
    {
        foreach (var outlier in new[] { Math.BitIncrement(1d), double.Epsilon, double.MaxValue,
            Math.Pow(2, -513), Math.Pow(2, 501) })
        {
            var values = Enumerable.Range(0, 2051).Select(i => (i % 17 - 8) / 4d).ToArray();
            values[2048] = outlier;
            var expected = new double[values.Length];
            MovingAverageCore.SimpleMovingAverage(values, expected, 20);
            var plan = FusedBarExecution.TryCreate(new IIndicator[] { new Sma(20), new PriceCircularTransform(PriceCircularOperation.ArcSine) }, values.Length)!;
            plan.Execute(BarsFor(values), new OwnedBarHistory(), default);
            Assert.True(plan.UsedSmaFallback);
            AssertBits(expected, plan.SmaValues!);
            for (var i = 0; i < values.Length; i++)
            {
                var defined = values[i] is >= -1 and <= 1;
                Assert.Equal(BitConverter.DoubleToInt64Bits(defined ? Math.Asin(values[i]) : 0),
                    BitConverter.DoubleToInt64Bits(plan.AsinValues![0][i]));
                Assert.Equal(defined ? 1d : 0d, plan.AsinValues[1][i]);
            }
        }
    }

    [Fact]
    public void ConservativeCertificateRefinesDifferentExponentAndQuantumMinima()
    {
        var values = Enumerable.Range(0, 2051).Select(i => i % 2 == 0 ? Math.Pow(2, -10) : Math.Pow(2, 20) + Math.Pow(2, -10)).ToArray();
        var plan = FusedBarExecution.TryCreate(new[] { new Sma(20) }, values.Length)!;
        plan.Execute(BarsFor(values), new OwnedBarHistory(), default);
        Assert.False(plan.UsedSmaFallback);
        var expected = new double[values.Length];
        MovingAverageCore.SimpleMovingAverage(values, expected, 20);
        AssertBits(expected, plan.SmaValues!);
    }

    [Fact]
    public void OverflowedSpeculationIsReplacedBeforePublication()
    {
        var values = new[] { double.MaxValue, double.MaxValue, -double.MaxValue, -double.MaxValue, 1d, 2d, -2d, double.Epsilon };
        var plan = FusedBarExecution.TryCreate(new[] { new Sma(2) }, values.Length)!;
        plan.Execute(BarsFor(values), new OwnedBarHistory(), default);
        Assert.True(plan.UsedSmaFallback);
        for (var i = 1; i < values.Length; i++)
        {
            var expected = ((ReferenceFraction.FromDouble(values[i - 1]) + ReferenceFraction.FromDouble(values[i])) / new ReferenceFraction(2)).ToDouble();
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(plan.SmaValues![i]));
        }
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 1)]
    [InlineData(1023, 0)]
    [InlineData(1024, -1)]
    [InlineData(1025, 20)]
    [InlineData(2051, int.MaxValue)]
    public async Task CombinedBuilderMatchesUnfusedAndRetainsSnapshots(int count, int period)
    {
        var values = Enumerable.Range(0, count).Select(i => i % 7 == 0 ? -0d : (i % 13 - 6) / 4d).ToArray();
        var bars = BarsFor(values);
        var sma = new Sma(period);
        var asin = new PriceCircularTransform(PriceCircularOperation.ArcSine);
        using var fused = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(sma, asin, sma).BuildAsync();
        using var ordinary = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars, bar => bar)).ConfigureIndicators(sma, asin, sma).BuildAsync();
        AssertBits(ordinary[sma].ToArray(), fused[sma].ToArray());
        AssertBits(ordinary[asin.Value].ToArray(), fused[asin.Value].ToArray());
        AssertBits(ordinary[asin.IsDefined].ToArray(), fused[asin.IsDefined].ToArray());
        var expected = bars.ToArray();
        Array.Clear(bars);
        var snapshots = new List<Bar>();
        await foreach (var snapshot in fused) snapshots.Add(snapshot.Bar);
        Assert.Equal(expected, snapshots);
    }

    [Fact]
    public void RuntimePublishesTheFusedArrayAndResolvesPriceAfterDisposal()
    {
        var bars = BarsFor(Enumerable.Range(0, 1031).Select(i => i / 4d).ToArray());
        var history = new OwnedBarHistory();
        var plan = FusedBarExecution.TryCreate(new[] { new Sma(20) }, bars.Length)!;
        plan.Execute(bars, history, default);
        var deferred = new Lazy<StockData>(() => throw new InvalidOperationException("Unexpected bridge"));
        SeriesHandle average = default, price = default;
        var builder = new StockIndicatorBuilder(IndicatorDataSource.FromValidatedHistory(deferred, history, plan))
            .ConfigureIndicators(catalog => { average = catalog.Sma(20); price = catalog.Price(); });
        IndicatorSnapshot snapshot;
        using (var runtime = builder.Build())
        {
            var updates = 0;
            runtime.Updated += _ => updates++;
            runtime.Start();
            runtime.Start();
            Assert.Equal(1, updates);
            snapshot = runtime.Latest!;
            Assert.True(MemoryMarshal.TryGetArray(snapshot.GetSeries(average), out var published));
            Assert.Same(plan.SmaValues, published.Array);
        }
        Assert.Equal(bars.Select(b => b.Close), snapshot.GetSeries(price).ToArray());
        Assert.False(deferred.IsValueCreated);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task FusedValidationPreservesDiagnosticsAndFailureOrder(int field)
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var bars = BarsFor(Enumerable.Repeat(.5, 2051).ToArray());
            var values = new[] { 1d, 1d, 1d, 1d, 1d };
            values[field] = bad;
            bars[1024] = new Bar(default, values[0], values[1], values[2], values[3], values[4]);
            bars[1025] = new Bar(default, double.NaN, 0, 0, 0, 0);
            Task<IIndicatorRun> Build(IBarSource source) => new StockIndicatorBuilder().ConfigureSource(source)
                .ConfigureIndicators(new Sma(20), new PriceCircularTransform(PriceCircularOperation.ArcSine)).BuildAsync();
            var expected = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Build(Bars.From(bars, bar => bar)));
            var actual = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Build(Bars.From(bars)));
            Assert.Equal(expected.Message, actual.Message);
        }
    }

    [Fact]
    public void ExposedMutableColumnsInvalidateFusedRuntimeReuse()
    {
        var input = Enumerable.Range(0, 25).Select(i => (double)i).ToArray();
        var history = new OwnedBarHistory();
        var plan = FusedBarExecution.TryCreate(new[] { new Sma(3) }, input.Length)!;
        plan.Execute(BarsFor(input), history, default);
        var columns = new Lazy<StockData>(() => new StockData(input, input, input, input,
            new double[input.Length], new DateTime[input.Length]));
        var source = IndicatorDataSource.FromValidatedHistory(columns, history, plan);
        source.BatchData!.ClosePrices[24] = 300;
        SeriesHandle average = default;
        using var runtime = new StockIndicatorBuilder(source).ConfigureIndicators(catalog => average = catalog.Sma(3)).Build();
        runtime.Start();
        Assert.Equal(115d, runtime.GetSeries(average)[24]);
        Assert.Equal(23d, plan.SmaValues![24]);
    }

    [Fact]
    public async Task FusedRunAndLaterLegacyBuildKeepOwnedInputAfterCallerMutation()
    {
        var input = Enumerable.Range(0, 25).Select(i => (double)i).ToArray();
        var bars = BarsFor(input);
        var sma = new Sma(3);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(sma);
        var run = await builder.BuildAsync();
        var snapshot = run.Latest;
        run.Dispose();
        Array.Clear(bars);
        SeriesHandle ema = default;
        using var legacy = builder.ConfigureIndicators(catalog => ema = catalog.Ema(3)).Build();
        legacy.Start();
        var expected = new double[input.Length];
        MovingAverageCore.ExponentialMovingAverage(input, expected, 3);
        AssertBits(expected, legacy.GetSeries(ema).ToArray());
        Assert.Equal(24d, snapshot.Bar.Close);
    }

    [Fact]
    public void ComposedAndUnsupportedGraphsAreNotSpeculativelyExecuted()
    {
        var chained = new Sma(3);
        chained.Of(new Sma(2));
        Assert.Null(FusedBarExecution.TryCreate(new IIndicator[] { chained }, 20));
        Assert.Null(FusedBarExecution.TryCreate(new IIndicator[] { new Ema(3) }, 20));
        Assert.Null(FusedBarExecution.TryCreate(new IIndicator[] { new PriceCircularTransform(PriceCircularOperation.Cosine) }, 20));
    }

    [Fact]
    public async Task PlainTypedRunPublishesDirectlyButLegacyConfigurationRetainsItsRuntime()
    {
        var bars = BarsFor(Enumerable.Range(0, 1031).Select(i => (i % 17 - 8) / 8d).ToArray());
        var sma = new Sma(20);
        var asin = new PriceCircularTransform(PriceCircularOperation.ArcSine);
        StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(sma, asin);
        using var direct = await Builder().BuildAsync();
        Assert.False(((IndicatorRun)direct).HasLegacyRuntime);
        var configurations = new Action<StockIndicatorBuilder>[]
        {
            b => b.ConfigureBehavior(_ => { }), b => b.ConfigureNotifications(), b => b.ConfigureAutoTrading(),
            b => b.ConfigureSignals(), b => b.ConfigureIndicators(new Builder.IndicatorOptions()),
            b => b.ConfigureData(new DataOptions()), b => b.ConfigureSymbols(new SymbolOptions()),
            b => b.ConfigureBacktesting(), b => b.ConfigureBenchmarking()
        };
        foreach (var configure in configurations)
        {
            var builder = Builder();
            configure(builder);
            using var legacy = await builder.BuildAsync();
            Assert.True(((IndicatorRun)legacy).HasLegacyRuntime);
            AssertBits(direct[sma].ToArray(), legacy[sma].ToArray());
            AssertBits(direct[asin.Value].ToArray(), legacy[asin.Value].ToArray());
            AssertBits(direct[asin.IsDefined].ToArray(), legacy[asin.IsDefined].ToArray());
        }
        var laterBuilder = Builder();
        using var initial = await laterBuilder.BuildAsync();
        laterBuilder.ConfigureIndicators(c => c.Sma(20));
        using var later = await laterBuilder.BuildAsync();
        Assert.True(((IndicatorRun)later).HasLegacyRuntime);
        AssertBits(direct[sma].ToArray(), later[sma].ToArray());
    }

    private static void AssertBits(double[] expected, double[] actual) => Assert.Equal(
        expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));

    [Fact]
    public async Task SynchronousLegacyCallbackCanChangeTypedSourcesAndAddIndicators()
    {
        async Task<(double[] Asin, double[] Added)> Build(bool projected)
        {
            var bars = BarsFor(Enumerable.Range(0, 30).Select(i => (i % 7) / 8d).ToArray());
            var asin = new PriceCircularTransform(PriceCircularOperation.ArcSine);
            var added = new Sma(3);
            var builder = new StockIndicatorBuilder().ConfigureSource(projected ? Bars.From(bars, b => b) : Bars.From(bars))
                .ConfigureIndicators(asin);
            using (await builder.BuildAsync()) { }
            SeriesHandle price = default;
            SignalHandle signal = default;
            builder.ConfigureIndicators(c => price = c.Price());
            builder.ConfigureSignals(s => signal = s.When(price).Above(-1).Emit("test callback"));
            var calls = 0;
            builder.ConfigureAutoTrading(t => t.AddAdapter(new CallbackAdapter(() =>
            {
                calls++;
                asin.Of(new Sma(2));
                builder.ConfigureIndicators(added);
            })).OnSignal(signal).MarketBuy());
            using var run = await builder.BuildAsync();
            Assert.Equal(1, calls);
            Assert.True(((IndicatorRun)run).HasLegacyRuntime);
            Assert.Equal(bars.Length, run[added].Length);
            return (run[asin.Value].ToArray(), run[added].ToArray());
        }
        var expected = await Build(true);
        var actual = await Build(false);
        AssertBits(expected.Asin, actual.Asin);
        AssertBits(expected.Added, actual.Added);
    }

    private sealed class CallbackAdapter(Action callback) : Builder.Trading.IAutoTradeAdapter
    {
        public void Execute(Builder.Trading.TradeRequest request) => callback();
    }
}

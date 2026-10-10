using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class CloseOnlyBuilderTests
{
    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 1)]
    [InlineData(1025, 7)]
    [InlineData(2051, 3000)]
    public void CloseOnlyGraphPreservesCsePublicationAndSnapshotLifetime(int count, int length)
    {
        var bars = Enumerable.Range(0, count).Select(i => new Bar(default, i, i, i, i / 4d, 0)).ToArray();
        var history = new OwnedBarHistory();
        foreach (var bar in bars) history.Add(bar);
        var deferred = new Lazy<StockData>(() => throw new InvalidOperationException("Unnecessary columns"));
        SeriesHandle first = default, duplicate = default, price = default;
        var builder = new StockIndicatorBuilder(IndicatorDataSource.FromValidatedHistory(deferred, history))
            .ConfigureIndicators(catalog =>
            {
                first = catalog.Sma(length);
                duplicate = catalog.Sma(length);
                price = catalog.Price();
            });
        using var runtime = builder.Build();
        var publications = 0;
        runtime.Updated += _ => publications++;
        runtime.Start();
        runtime.Start();
        Assert.Equal(first, duplicate);
        Assert.Equal(1, publications);
        Assert.False(deferred.IsValueCreated);
        var snapshot = runtime.Latest!;
        runtime.Dispose();
        var closes = bars.Select(b => b.Close).ToArray();
        var expected = new double[count];
        MovingAverageCore.SimpleMovingAverage(closes, expected, length);
        Assert.Equal(expected, snapshot.GetSeries(first).ToArray());
        Assert.Equal(closes, snapshot.GetSeries(price).ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MixedGraphsAndExposedColumnsUseTheOrdinaryEvaluator(bool expose)
    {
        var history = new OwnedBarHistory();
        for (var i = 0; i < 20; i++) history.Add(new Bar(default, i, i, i, i, 0));
        var close = Enumerable.Range(0, 20).Select(i => (double)i).ToArray();
        var deferred = new Lazy<StockData>(() => new StockData(close, close, close, close,
            new double[20], new DateTime[20]));
        var source = IndicatorDataSource.FromValidatedHistory(deferred, history);
        if (expose) source.BatchData!.ClosePrices[19] = 100;
        SeriesHandle average = default;
        using var runtime = new StockIndicatorBuilder(source).ConfigureIndicators(catalog =>
        {
            average = catalog.Sma(3);
            if (!expose) catalog.Ema(3);
        }).Build();
        runtime.Start();
        Assert.True(deferred.IsValueCreated);
        Assert.Equal(expose ? 45d : 18d, runtime.Latest!.GetSeries(average).Span[19]);
    }

    [Fact]
    public void DeferredNonSmaLookupSurvivesRuntimeDisposal()
    {
        var close = Enumerable.Range(0, 30).Select(i => (double)i).ToArray();
        var history = new OwnedBarHistory();
        foreach (var value in close) history.Add(new Bar(default, value, value, value, value, 0));
        var columns = new Lazy<StockData>(() => new StockData(close, close, close, close,
            new double[close.Length], new DateTime[close.Length]));
        SeriesHandle active = default, deferred = default;
        var builder = new StockIndicatorBuilder(IndicatorDataSource.FromValidatedHistory(columns, history))
            .ConfigureIndicators(catalog => { active = catalog.Sma(3); deferred = catalog.Ema(3); });
        builder.ConfigureSignals(signals => signals.When(active).CrossesAbove(0).Emit("active"));
        IndicatorSnapshot snapshot;
        using (var runtime = builder.Build())
        {
            runtime.Start();
            snapshot = runtime.Latest!;
            Assert.False(columns.IsValueCreated);
        }
        var expected = new double[close.Length];
        MovingAverageCore.ExponentialMovingAverage(close, expected, 3);
        Assert.Equal(expected, snapshot.GetSeries(deferred).ToArray());
        Assert.True(columns.IsValueCreated);
        Assert.Equal(expected, snapshot.GetSeries(deferred).ToArray());
    }

    [Fact]
    public async Task TypedSmaAvoidsLegacyColumnAllocationAndKeepsIndependentRuns()
    {
        var bars = Enumerable.Range(0, 10000).Select(i => new Bar(default, i, i, i, i, 0)).ToArray();
        var sma = new Sma(14);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(sma);
        using (await builder.BuildAsync()) { }
        var before = GC.GetAllocatedBytesForCurrentThread();
        using var first = await builder.BuildAsync();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 850000, $"Allocated {allocated:N0} bytes");
        var saved = first[sma].ToArray();
        bars[^1] = new Bar(default, 0, 0, 0, 0, 0);
        using var second = await builder.BuildAsync();
        Assert.NotEqual(saved[^1], second[sma][^1]);
        Assert.Equal(saved, first[sma].ToArray());
    }
}

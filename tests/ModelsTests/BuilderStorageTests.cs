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
        // Owned history, six columns and two output columns fit below this bound.
        // The former geometrically-grown history/columns and duplicate copies exceeded 3 MB.
        Assert.True(allocated < 1_500_000, $"Allocated {allocated:N0} bytes for one fresh builder.");
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

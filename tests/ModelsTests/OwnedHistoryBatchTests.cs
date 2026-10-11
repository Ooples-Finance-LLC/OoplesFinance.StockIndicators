using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class OwnedHistoryBatchTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(1023)] [InlineData(1024)]
    [InlineData(1025)] [InlineData(10000)] [InlineData(32769)]
    public void FusedChunkStoragePreservesParallelWritesWithoutLargeBarArrays(int count)
    {
        var buffer = new OwnedBarBuffer(count);
        var expected = Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i),
            i, -i, double.Epsilon, i % 2 == 0 ? -0d : 0d, double.MaxValue)).ToArray();
        Parallel.For(0, count, i => buffer[i] = expected[i]);
        var history = new OwnedBarHistory();
        buffer.TransferTo(history);
        Assert.Equal(expected, history.ToArray());
        var flattened = new List<Bar>();
        for (int i = 0; i < history.ChunkCount; i++)
        {
            Assert.InRange(history.Chunk(i).Length, 1, 1024);
            flattened.AddRange(history.Chunk(i).ToArray());
        }
        Assert.Equal(expected, flattened);
        for (int i = 0; i < count; i++)
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i].Close), BitConverter.DoubleToInt64Bits(history[i].Close));
        Assert.Throws<InvalidOperationException>(() => history.Add(default));
        Assert.Throws<InvalidOperationException>(() => new OwnedBarBuffer(0).TransferTo(history));
        var later = new OwnedBarBuffer(count);
        for (int i = 0; i < count; i++) later[i] = default;
        Assert.Equal(expected, history.ToArray());
    }

    [Fact]
    public void ContiguousHistoryPreservesChunkViewsSlicesAndEnumeration()
    {
        var expected = Enumerable.Range(0, 32769).Select(i => new Bar(default, i, i, i, i, i)).ToArray();
        var history = new OwnedBarHistory();
        history.TakeOwnedArray(expected);
        Assert.Equal(expected, history.ToArray());
        Assert.Equal(expected.Skip(1023), history.AfterWarmup(1023));
        var flattened = new List<Bar>();
        for (var i = 0; i < history.ChunkCount; i++) flattened.AddRange(history.Chunk(i).ToArray());
        Assert.Equal(expected, flattened);
        for (var i = 0; i < expected.Length; i++) Assert.Equal(expected[i], history[i]);
        Assert.Throws<ArgumentOutOfRangeException>(() => history[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => history[history.Count]);
        Assert.Throws<InvalidOperationException>(() => history.Add(default));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(17)]
    [InlineData(1023)]
    [InlineData(1024)]
    [InlineData(1025)]
    public void BulkAppendOwnsCopiesAndSupportsPartialChunksAndLaterAppends(int prefix)
    {
        var expected = Enumerable.Range(0, prefix + 2051).Select(i => new Bar(default, i, i, i, i, i)).ToArray();
        var history = new OwnedBarHistory();
        foreach (var bar in expected.Take(prefix)) history.Add(bar);
        var source = expected.Skip(prefix).ToArray();
        history.AppendValidated(source, default);
        Array.Clear(source);
        history.Add(new Bar(default, -1, -1, -1, -1, -1));
        Assert.Equal(expected.Append(new Bar(default, -1, -1, -1, -1, -1)), history.ToArray());
        for (var i = 0; i < expected.Length; i++) Assert.Equal(expected[i], history[i]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(1023)]
    [InlineData(1024)]
    [InlineData(1025)]
    [InlineData(2051)]
    [InlineData(16383)]
    [InlineData(16384)]
    [InlineData(32769)]
    public async Task ArrayAndProjectedBuildersHaveIdenticalOutputsAndOwnedSnapshots(int count)
    {
        var bars = Enumerable.Range(0, count).Select(i =>
        {
            var close = i % 13 == 0 ? -0d : (i % 11 - 5) / 4d;
            return new Bar(DateTime.UnixEpoch.AddMinutes(i), double.MaxValue, double.Epsilon, -double.MaxValue, close, -0d);
        }).ToArray();
        var sma = new Sma(20);
        var asin = new PriceCircularTransform(PriceCircularOperation.ArcSine);
        var projected = 0;
        using var bulk = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(sma, asin).BuildAsync();
        using var scalar = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars, bar => { projected++; return bar; }))
            .ConfigureIndicators(sma, asin).BuildAsync();
        Assert.Equal(count, projected);
        foreach (var output in new[] { sma.Outputs[0], asin.Value, asin.IsDefined })
            Assert.Equal(scalar[output].ToArray().Select(BitConverter.DoubleToInt64Bits),
                bulk[output].ToArray().Select(BitConverter.DoubleToInt64Bits));
        var expected = bars.ToArray();
        Array.Clear(bars);
        var snapshots = new List<Bar>();
        await foreach (var snapshot in bulk) snapshots.Add(snapshot.Bar);
        Assert.Equal(expected, snapshots);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task BulkValidationPreservesFirstInvalidFieldAndBar(int field)
    {
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var index in new[] { 0, 1023, 1024, 2050 })
        {
            var fields = new[] { 1d, 1d, 1d, 1d, 1d };
            fields[field] = invalid;
            var bars = Enumerable.Repeat(new Bar(default, 1, 1, 1, 1, 1), 2052).ToArray();
            bars[index] = new Bar(default, fields[0], fields[1], fields[2], fields[3], fields[4]);
            bars[index + 1] = new Bar(default, double.NaN, 1, 1, 1, 1);
            var expected = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                new StockIndicatorBuilder().ConfigureSource(Bars.From(bars, bar => bar)).BuildAsync());
            var actual = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).BuildAsync());
            Assert.Equal(expected.Message, actual.Message);
        }
    }

    [Fact]
    public async Task CancelledArrayBuildDoesNotPreventReusingTheSource()
    {
        var source = Bars.From(new[] { new Bar(default, 1, 1, 1, 1, 1) });
        var builder = new StockIndicatorBuilder().ConfigureSource(source).ConfigureIndicators(new Sma(1));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancellation.Token));
        using var run = await builder.BuildAsync();
        Assert.Equal(1, run.BarCount);
        Assert.Equal(1d, run.Latest.Bar.Close);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    [InlineData(1023)]
    [InlineData(1024)]
    [InlineData(1025)]
    [InlineData(10000)]
    public void GrowingAndHintedHistoryPreserveEveryBoundary(int hint)
    {
        var history = new OwnedBarHistory();
        history.ExpectAdditional(hint);
        var expected = Enumerable.Range(0, 4099).Select(i => new Bar(default, i, i, i, i, i)).ToArray();
        foreach (var bar in expected) history.Add(bar);
        Assert.Equal(expected, history.ToArray());
        for (var i = 0; i < expected.Length; i++) Assert.Equal(expected[i], history[i]);
        for (var i = 0; i < history.ChunkCount; i++) Assert.True(history.Chunk(i).Length <= 1024);
        Assert.Throws<ArgumentOutOfRangeException>(() => history[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => history[history.Count]);
    }

    [Fact]
    public async Task AsinBatchRetainsPresenceSignedZeroWarmupAndChainedFallback()
    {
        var closes = Enumerable.Range(0, 4099).Select(i => (i % 9 - 4) / 3d).ToArray();
        closes[1023] = -0d;
        closes[2048] = -0d;
        var bars = closes.Select(c => new Bar(default, c, c, c, c, 1)).ToArray();
        var asin = new PriceCircularTransform(PriceCircularOperation.ArcSine);
        var chained = new PriceCircularTransform(PriceCircularOperation.ArcSine);
        chained.Of(new EchoClose());
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars.Skip(1025)).WarmedWith(bars.Take(1025)))
            .ConfigureIndicators(asin, chained);
        using var run = await builder.BuildAsync();
        for (var i = 1025; i < closes.Length; i++)
        {
            var present = Math.Abs(closes[i]) <= 1;
            var expected = present ? Math.Asin(closes[i]) : 0;
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(run[asin.Value][i - 1025]));
            Assert.Equal(present ? 1d : 0d, run[asin.IsDefined][i - 1025]);
            Assert.Equal(run[asin.Value][i - 1025], run[chained.Value][i - 1025]);
        }
        var published = new List<Bar>();
        await foreach (var snapshot in run) published.Add(snapshot.Bar);
        Assert.Equal(bars.Skip(1025), published);
        bars[^1] = new Bar(default, 99, 99, 99, 99, 0);
        Assert.Equal(closes[^1], run.Latest.Bar.Close);
    }

    [Fact]
    public async Task RickshawBatchMatchesSequentialStateAcrossChunkAndFallbackTransitions()
    {
        var indicator = new RickshawManCandle();
        var bars = Enumerable.Range(0, 2051).Select(i => new Bar(default, 100, 120, 80, 100 + (i % 3) / 4d, 0)).ToArray();
        bars[1024] = new Bar(default, double.Epsilon, double.MaxValue, -double.MaxValue, 0, 0);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        var state = (IIndicatorState)indicator.CreateState();
        for (var i = 0; i < bars.Length; i++) Assert.Equal(state.Update(bars[i]), run[indicator][i]);
    }

    private sealed class EchoClose : IndicatorBase
    {
        protected internal override object CreateState() => new State();
        private sealed class State : IIndicatorState
        {
            public void Reset() { }
            public double Update(in Bar bar) => bar.Close;
        }
    }
}

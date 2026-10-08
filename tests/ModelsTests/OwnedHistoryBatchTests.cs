using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class OwnedHistoryBatchTests
{
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

using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class NativeAliasOutputTests
{
    private static Bar[] BarsForTest() => new[] { 1d, 3, 2, 4, 1, 5 }
        .Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, 1)).ToArray();

    [Fact]
    public async Task FiniteChainedAliasesSelectTheirNamedOutput()
    {
        var source = new Sma(2);
        var stochastic = new StochasticOscillator(3);
        var d = new StochasticD(3);
        var chande = new ChandeMomentumOscillator(3, 2);
        var signal = new ChandeMomentumOscillatorSignal(3, 2);
        stochastic.Of(source); d.Of(source); chande.Of(source); signal.Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(BarsForTest()))
            .ConfigureIndicators(source, stochastic, d, chande, signal).BuildAsync();
        Assert.Equal(run[stochastic.FastD].ToArray(), run[d].ToArray());
        Assert.Equal(run[chande.Signal].ToArray(), run[signal].ToArray());
        Assert.False(run[stochastic].ToArray().SequenceEqual(run[d].ToArray()));
        Assert.False(run[chande].ToArray().SequenceEqual(run[signal].ToArray()));
    }

    [Fact]
    public async Task LiveAliasesSelectTheirNamedOutput()
    {
        var source = Bars.Live();
        var stochastic = new StochasticOscillator(3);
        var d = new StochasticD(3);
        var chande = new ChandeMomentumOscillator(3, 2);
        var signal = new ChandeMomentumOscillatorSignal(3, 2);
        using var run = await new StockIndicatorBuilder().ConfigureSource(source).PublishBeforeWarmup()
            .ConfigureIndicators(stochastic, d, chande, signal).BuildAsync();
        var bars = BarsForTest();
        foreach (var bar in bars) source.Publish(bar);
        source.Complete();
        var count = 0;
        var differs = false;
        await foreach (var snapshot in run)
        {
            Assert.Equal(snapshot[stochastic.FastD], snapshot[d]);
            Assert.Equal(snapshot[chande.Signal], snapshot[signal]);
            differs |= snapshot[stochastic] != snapshot[d];
            count++;
        }
        Assert.Equal(bars.Length, count);
        Assert.True(differs);
    }

    [Fact]
    public void MissingNamedOutputCannotFallBackToAnUnrelatedPrimary()
    {
        foreach (var outputs in new IReadOnlyDictionary<string, double>?[] { null, new Dictionary<string, double> { ["FastK"] = 99 } })
            Assert.Throws<InvalidOperationException>(() => IndicatorContract.NativePrimary(new StochasticD(3), new(99, outputs)));
    }
}

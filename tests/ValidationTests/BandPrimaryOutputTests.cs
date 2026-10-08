using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class BandPrimaryOutputTests
{
    [Theory]
    [InlineData(false, 2)]
    [InlineData(false, 14)]
    [InlineData(true, 2)]
    [InlineData(true, 14)]
    public void UnnamedRoutesUseThePublishedPrimaryAndRetainNamedBands(bool movingBands, int length)
    {
        var prices = Enumerable.Range(0, 40).Select(i => 100d + (i % 7) * 3).ToArray();
        StockData Data() => new(prices, prices.Select(v => v + 2), prices.Select(v => v - 1), prices, prices.Select(_ => 1d), prices.Select((_, i) => DateTime.UnixEpoch.AddMinutes(i)));
        IIndicatorSpecOptions options = movingBands ? new MovingAverageBandsSpecOptions(length, length * 2, 1) : new AverageTrueRangeChannelSpecOptions(length, 2);
        var name = movingBands ? IndicatorName.MovingAverageBands : IndicatorName.AverageTrueRangeChannel;
        var key = movingBands ? "FastMa" : "UpperBand";
        var batch = movingBands ? Data().CalculateMovingAverageBands(fastLength: length, slowLength: length * 2) : Data().CalculateAverageTrueRangeChannel(length: length, mult: 2);
        var expected = batch.OutputValues[key];
        Assert.NotEqual(batch.OutputValues[movingBands ? "UpperBand" : "Sma"].ToArray(), expected.ToArray());
        using var context = new ComputeContext();
        foreach (var output in new string?[] { null, key, "MiddleBand", "LowerBand" })
        {
            var spec = output is null ? new IndicatorSpec(name, options) : new IndicatorSpec(name, options, output); using var actual = IndicatorCompute.TryComputeFast(Data(), spec, context);
            Assert.NotNull(actual); Assert.Equal(batch.OutputValues[output ?? key], actual.Value.ToArray());
        }
        IStreamingIndicatorState state = movingBands ? new MovingAverageBandsState(fastLength: length, slowLength: length * 2) : new AverageTrueRangeChannelState(length: length, mult: 2);
        using var lifetime = (IDisposable)state;
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < prices.Length; i++) foreach (var final in new[] { false, true })
            {
                var bar = new OhlcvBar("PRIMARY", BarTimeframe.Tick, DateTime.UnixEpoch, DateTime.UnixEpoch, prices[i], prices[i] + 2, prices[i] - 1, prices[i], 1, true);
                var value = state.Update(bar, final, true); Assert.Equal(expected[i], value.Value); Assert.Equal(expected[i], value.Outputs![key]);
            }
        }
    }
}

using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class KaufmanAdaptiveBandsDomainTests
{
    private static StockData Data() => new(new[] { 2d, 3, 1 }, new[] { 2d, 3, 1 }, new[] { 2d, 3, 1 }, new[] { 2d, 3, 1 }, new[] { 1d, 1, 1 }, Enumerable.Range(0, 3).Select(i => DateTime.UnixEpoch.AddMinutes(i)));
    [Theory]
    [InlineData(-1)]
    [InlineData(-double.Epsilon)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void InvalidExponentsAreRejectedAcrossEntryPoints(double exponent)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new KaufmanAdaptiveBandsSpecOptions(stdDevFactor: exponent));
        Assert.Throws<ArgumentOutOfRangeException>(() => new KaufmanAdaptiveBandsState(stdDevFactor: exponent));
        var data = Data(); data.SetCustomValues(new List<double> { 7, 8, 9 });
        Assert.Throws<ArgumentOutOfRangeException>(() => data.CalculateKaufmanAdaptiveBands(stdDevFactor: exponent));
        Assert.Equal(new[] { 7d, 8, 9 }, data.ChainedValues);
        Assert.Equal(new[] { 2d, 3, 1 }, data.ClosePrices);
    }
    [Fact]
    public void ZeroExponentRetainsUnitWeightsIncludingZeroEfficiency()
    {
        Assert.Equal(0, new KaufmanAdaptiveBandsSpecOptions(stdDevFactor: 0).StdDevFactor);
        var data = Data().CalculateKaufmanAdaptiveBands(2, 0);
        foreach (var key in new[] { "UpperBand", "MiddleBand", "LowerBand" }) Assert.Equal(new[] { 2d, 3, 1 }, data.OutputValues[key]);
        using var state = new KaufmanAdaptiveBandsState(2, 0);
        foreach (var price in new[] { 2d, 3, 1 })
        {
            var time = DateTime.UnixEpoch;
            var bar = new OhlcvBar("KAB", BarTimeframe.Minutes(1), time, time, price, price, price, price, 1, true);
            var actual = state.Update(bar, true, true);
            Assert.Equal(price, actual.Value);
            foreach (var key in new[] { "UpperBand", "MiddleBand", "LowerBand" }) Assert.Equal(price, actual.Outputs![key]);
        }
    }
    [Theory]
    [InlineData(double.Epsilon)]
    [InlineData(0.5)]
    [InlineData(double.MaxValue)]
    public void FinitePositiveExponentsRemainAccepted(double exponent)
    {
        Assert.Equal(exponent, new KaufmanAdaptiveBandsSpecOptions(stdDevFactor: exponent).StdDevFactor);
        using var state = new KaufmanAdaptiveBandsState(stdDevFactor: exponent);
        Data().CalculateKaufmanAdaptiveBands(stdDevFactor: exponent);
    }
}

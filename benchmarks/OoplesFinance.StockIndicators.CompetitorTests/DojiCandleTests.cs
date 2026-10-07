using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class DojiCandleTests
{
    public static IEnumerable<object[]> Examples()
    {
        yield return new object[] { 0d, 10d, 0d, 1d, 0.1m, 0d };
        yield return new object[] { 0d, 10d, 0d, Math.BitDecrement(1d), 0.1m, 1d };
        yield return new object[] { 0d, 10d, 0d, Math.BitIncrement(1d), 0.1m, 0d };
        yield return new object[] { 1d, 10d, 0d, 0d, 0.1m, 0d };
        yield return new object[] { 1d, 10d, 0d, 1d, 0.1m, 1d };
        yield return new object[] { 1d, 1d, 1d, 1d, 0.1m, 0d };
        yield return new object[] { 0d, 10 * double.Epsilon, 0d, double.Epsilon, 0.1m, 0d };
        yield return new object[] { 0d, 11 * double.Epsilon, 0d, double.Epsilon, 0.1m, 1d };
        yield return new object[] { 0d, double.MaxValue, -double.MaxValue, double.Epsilon, 0.1m, 1d };
        yield return new object[] { -double.MaxValue, double.MaxValue, -double.MaxValue, double.MaxValue, 1m, 0d };
        yield return new object[] { 0d, 10d, 0d, 0d, 0m, 0d };
        yield return new object[] { 0d, 4d, 0d, 1d, 0.25m, 0d };
        yield return new object[] { 0d, 1d, 0d, 0.125d, 0.1234567890123456789012345678m, 0d };
    }

    [Theory]
    [MemberData(nameof(Examples))]
    public async Task StrictDecimalBoundarySurvivesExtremeAndTinyPrices(double open, double high, double low,
        double close, decimal threshold, double expected)
    {
        var indicator = new DojiCandle(threshold);
        var bars = new[] { new Bar(DateTime.UnixEpoch, open, high, low, close, 1) };
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(expected, Assert.Single(run[indicator].ToArray()));
    }

    [Fact]
    public void RejectsFractionsOutsideUnitInterval()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DojiCandle(-0.1m));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DojiCandle(1.1m));
    }
}

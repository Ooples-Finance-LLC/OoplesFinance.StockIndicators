using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class PeriodicChannelCertifiedTests
{
    private static void Same((double[] Values, Signal Trade) expected, (double[] Values, Signal Trade) actual)
    {
        Assert.Equal(expected.Values.Select(BitConverter.DoubleToInt64Bits), actual.Values.Select(BitConverter.DoubleToInt64Bits));
        Assert.Equal(expected.Trade, actual.Trade);
    }
    [Theory]
    [InlineData(100, 500)]
    [InlineData(1, 1)]
    [InlineData(7, 3)]
    public void AllBandsTradesPreviewsAndResetMatchExact(int period, int lookback)
    {
        using var candidate = new PeriodicChannelWindow(period, lookback); using var exact = new PeriodicChannelWindow(period, lookback, false);
        for (var pass = 0; pass < 2; pass++)
        {
            for (var i = 0; i < 60; i++)
            {
                var p = 10 + (i * 13 % 97) / 8d;
                Same(exact.Next(-p, false), candidate.Next(-p, false));
                Same(exact.Next(p, false), candidate.Next(p, false));
                Same(exact.Next(p, true), candidate.Next(p, true));
            }
            candidate.Reset(); exact.Reset(); Assert.Equal(0, candidate.CertifiedCount); Assert.Equal(0, candidate.FallbackCount);
        }
    }
    [Fact]
    public void LongVaryingHistoryCertifiesEveryBandAndTrade()
    {
        using var candidate = new PeriodicChannelWindow(100, 500);
        for (var i = 0; i < 10000; i++)
        {
            var p = 10 + (i * 13 % 101) / 8d;
            var point = candidate.Next(p, true);
            Assert.All(point.Values, v => Assert.True(double.IsFinite(v)));
            Assert.Equal(0, candidate.FallbackCount);
        }
        Assert.Equal(10000, candidate.CertifiedCount);
    }
    [Fact]
    public void ZeroHistoryHasIndependentExactOutputs()
    {
        using var candidate = new PeriodicChannelWindow(7, 3);
        for (var i = 0; i < 10000; i++)
        {
            Same((new double[8], Signal.None), candidate.Next(0, true));
            Assert.Equal(0, candidate.FallbackCount);
        }
    }
    [Fact]
    public void AmbiguousCancellationPreviewReplaysOnlyCommittedBars()
    {
        using var candidate = new PeriodicChannelWindow(7, 3); using var exact = new PeriodicChannelWindow(7, 3, false);
        Same(exact.Next(double.Epsilon, true), candidate.Next(double.Epsilon, true));
        Same(exact.Next(1, false), candidate.Next(1, false));
        Assert.Equal(1, candidate.FallbackCount);
        Same(exact.Next(2, true), candidate.Next(2, true));
        candidate.Reset(); exact.Reset();
        Same(exact.Next(1, true), candidate.Next(1, true));
        Assert.Equal(0, candidate.FallbackCount); Assert.Equal(1, candidate.CertifiedCount);
    }
    [Fact]
    public void InvalidPreviewDoesNotMutateCorrelationOrArithmetic()
    {
        using var candidate = new PeriodicChannelWindow(7, 3); using var exact = new PeriodicChannelWindow(7, 3, false);
        Same(exact.Next(1, true), candidate.Next(1, true));
        Assert.Throws<ArgumentOutOfRangeException>(() => candidate.Next(double.NaN, false));
        Same(exact.Next(3, true), candidate.Next(3, true));
    }
}

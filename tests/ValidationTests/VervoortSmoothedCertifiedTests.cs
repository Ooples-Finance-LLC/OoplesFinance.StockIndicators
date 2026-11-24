using OoplesFinance.StockIndicators.Helpers;
using Number = OoplesFinance.StockIndicators.Helpers.PeriodicChannelWindow.Number;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class VervoortSmoothedCertifiedTests
{
    private static void Same((double Line, double Stochastic, Signal Trade) a, (double Line, double Stochastic, Signal Trade) b)
    {
        Assert.Equal(BitConverter.DoubleToInt64Bits(a.Line), BitConverter.DoubleToInt64Bits(b.Line));
        Assert.Equal(BitConverter.DoubleToInt64Bits(a.Stochastic), BitConverter.DoubleToInt64Bits(b.Stochastic)); Assert.Equal(a.Trade, b.Trade);
    }
    [Theory]
    [InlineData(2)]
    [InlineData(-2)]
    [InlineData(0)]
    [InlineData(1.6)]
    public void BandsStochasticTradesPreviewsAndResetMatchExact(double multiplier)
    {
        using var candidate = new VervoortSmoothedWindow(7, 9, 2, 3, multiplier);
        using var exact = new VervoortSmoothedWindow(7, 9, 2, 3, multiplier, false);
        for (var pass = 0; pass < 2; pass++)
        {
            for (var i = 0; i < 60; i++)
            {
                var p = 10 + (i * 13 % 97) / 8d;
                Same(exact.Next(-p, -p, p+2, p-1, false), candidate.Next(-p, -p, p+2, p-1, false));
                Same(exact.Next(p, p+0.25, p+2, p-1, true), candidate.Next(p, p+0.25, p+2, p-1, true));
            }
            candidate.Reset(); exact.Reset(); Assert.Equal(0, candidate.CertifiedCount); Assert.Equal(0, candidate.FallbackCount);
        }
    }
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(int.MaxValue)]
    public void BoundaryPeriodsKeepExactOutputs(int period)
    {
        using var candidate = new VervoortSmoothedWindow(period, period, period, period, -2);
        using var exact = new VervoortSmoothedWindow(period, period, period, period, -2, false);
        foreach (var p in new[] { 0d, double.Epsilon, -double.Epsilon, 1d, -1d, 2d })
            Same(exact.Next(p,p,Math.Abs(p)+1,-Math.Abs(p)-1,true),candidate.Next(p,p,Math.Abs(p)+1,-Math.Abs(p)-1,true));
    }
    [Fact]
    public void LongVaryingHistoryCertifiesEveryBand()
    {
        using var candidate = new VervoortSmoothedWindow(18, 30, 2, 3, 2);
        for (var i = 0; i < 10000; i++)
        {
            var p = 10 + (i * 13 % 101) / 8d;
            var point = candidate.Next(p,p-1d/3,p+1,p-2,true);
            Assert.True(double.IsFinite(point.Line)); Assert.True(double.IsFinite(point.Stochastic)); Assert.Equal(0,candidate.FallbackCount);
        }
        Assert.Equal(10000,candidate.CertifiedCount);
    }
    [Fact]
    public void ZeroHistoryHasIndependentExactOutputs()
    {
        using var candidate = new VervoortSmoothedWindow(18,30,2,3,2);
        for(var i=0;i<10000;i++) Same((0,0,Signal.None),candidate.Next(0,0,0,0,true));
        Assert.Equal(10000,candidate.CertifiedCount); Assert.Equal(0,candidate.FallbackCount);
    }
    [Fact]
    public void AmbiguousVarianceThresholdDoesNotCommitProposal()
    {
        var candidate = new CertifiedVervoortSmoothed(2,1,2); var control = new CertifiedVervoortSmoothed(2,1,2);
        var almost = Number.Of(1) - Number.Of(Math.Pow(2,-45)) + Number.Of(Math.Pow(2,-200));
        Assert.True(candidate.TryNext(almost,true,out _)); Assert.True(control.TryNext(almost,true,out _));
        Assert.False(candidate.TryNext(Number.Of(1),false,out _));
        Assert.True(candidate.TryNext(Number.Of(2),true,out var actual)); Assert.True(control.TryNext(Number.Of(2),true,out var expected));
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected),BitConverter.DoubleToInt64Bits(actual));
    }
    [Fact]
    public void InvalidPreviewLeavesStateUnchanged()
    {
        using var candidate = new VervoortSmoothedWindow(7,9,2,3,2); using var exact = new VervoortSmoothedWindow(7,9,2,3,2,false);
        Same(exact.Next(1,1,2,0,true),candidate.Next(1,1,2,0,true));
        Assert.ThrowsAny<ArgumentException>(()=>candidate.Next(double.NaN,1,2,0,false));
        Same(exact.Next(3,3,4,1,true),candidate.Next(3,3,4,1,true));
    }
}

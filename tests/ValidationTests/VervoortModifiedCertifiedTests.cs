using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class VervoortModifiedCertifiedTests
{
    private static void Same((double Percent,double Upper,double Lower,Signal Trade) expected,
        (double Percent,double Upper,double Lower,Signal Trade) actual)
    {
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Percent),BitConverter.DoubleToInt64Bits(actual.Percent));
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Upper),BitConverter.DoubleToInt64Bits(actual.Upper));
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Lower),BitConverter.DoubleToInt64Bits(actual.Lower));
        Assert.Equal(expected.Trade,actual.Trade);
    }
    [Theory]
    [InlineData(MovingAvgType.ExponentialMovingAverage)]
    [InlineData(MovingAvgType.WildersSmoothingMethod)]
    [InlineData(MovingAvgType.DoubleExponentialMovingAverage)]
    [InlineData(MovingAvgType.TripleExponentialMovingAverage)]
    [InlineData(MovingAvgType.SimpleMovingAverage)]
    public void ExactBitsSignalsPreviewsAndReset(MovingAvgType kind)
    {
        using var candidate=new VervoortModifiedWindow(kind,7,5,3,1.6);
        using var exact=new VervoortModifiedWindow(kind,7,5,3,1.6,false);
        for(var pass=0;pass<2;pass++)
        {
            for(var i=0;i<60;i++)
            {
                var price=10+(i*13%97)/8d;
                Same(exact.Next(-price,2,-1,false),candidate.Next(-price,2,-1,false));
                Same(exact.Next(price,price+2,price-1,false),candidate.Next(price,price+2,price-1,false));
                Same(exact.Next(price,price+2,price-1,true),candidate.Next(price,price+2,price-1,true));
            }
            candidate.Reset();exact.Reset();Assert.Equal(0,candidate.CertifiedCount);Assert.Equal(0,candidate.FallbackCount);
        }
    }
    [Theory]
    [InlineData(MovingAvgType.ExponentialMovingAverage)]
    [InlineData(MovingAvgType.WildersSmoothingMethod)]
    [InlineData(MovingAvgType.DoubleExponentialMovingAverage)]
    [InlineData(MovingAvgType.TripleExponentialMovingAverage)]
    public void LongVaryingHistoryCertifiesEveryBar(MovingAvgType kind)
    {
        using var candidate=new VervoortModifiedWindow(kind,18,200,8,1.6);
        for(var i=0;i<10000;i++)
        {
            var price=10+(i*13%101)/8d;
            var point=candidate.Next(price-0.25,price+1,price-2,true);
            Assert.True(double.IsFinite(point.Percent));Assert.True(double.IsFinite(point.Upper));Assert.True(double.IsFinite(point.Lower));
        }
        Assert.Equal(10000,candidate.CertifiedCount);Assert.Equal(0,candidate.FallbackCount);
    }
    [Theory]
    [InlineData(MovingAvgType.ExponentialMovingAverage)]
    [InlineData(MovingAvgType.TripleExponentialMovingAverage)]
    public void LongZeroHistoryHasIndependentOutputs(MovingAvgType kind)
    {
        using var candidate=new VervoortModifiedWindow(kind,18,200,8,1.6);
        for(var i=0;i<10000;i++)Same((0,50,50,i==0?Signal.StrongSell:Signal.Sell),candidate.Next(0,0,0,true));
        Assert.Equal(10000,candidate.CertifiedCount);Assert.Equal(0,candidate.FallbackCount);
    }
    [Fact]
    public void InvalidPreviewDoesNotMutateCommittedHistory()
    {
        using var candidate=new VervoortModifiedWindow(MovingAvgType.TripleExponentialMovingAverage,3,5,3,1.6);
        using var exact=new VervoortModifiedWindow(MovingAvgType.TripleExponentialMovingAverage,3,5,3,1.6,false);
        for(var i=0;i<20;i++)
        {
            Assert.ThrowsAny<ArgumentException>(()=>candidate.Next(double.NaN,2,0,false));
            Same(exact.Next(i,2,0,true),candidate.Next(i,2,0,true));
        }
    }
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(int.MaxValue)]
    public void BoundaryPeriodsKeepExactResults(int period)
    {
        using var candidate=new VervoortModifiedWindow(MovingAvgType.TripleExponentialMovingAverage,period,period,period,-2);
        using var exact=new VervoortModifiedWindow(MovingAvgType.TripleExponentialMovingAverage,period,period,period,-2,false);
        foreach(var value in new[]{0d,double.Epsilon,-double.Epsilon,1d,-1d,2d})
            Same(exact.Next(value,Math.Abs(value)+1,-Math.Abs(value)-1,true),candidate.Next(value,Math.Abs(value)+1,-Math.Abs(value)-1,true));
    }
}

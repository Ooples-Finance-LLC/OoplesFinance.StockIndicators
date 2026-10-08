using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class AnticipatePeriodLimitTests
{
    private static StockData Data() => new(new[]{1d},new[]{2d},new[]{0d},new[]{1d},new[]{1d},new[]{DateTime.UnixEpoch});
    [Theory]
    [InlineData(4097)]
    [InlineData(int.MaxValue)]
    public void OversizedPeriodsRejectAcrossAllRoutesBeforePublishing(int length)
    {
        foreach(var kind in new[]{MovingAvgType.EhlersHannMovingAverage,MovingAvgType.DoubleExponentialMovingAverage})
        {
            Assert.Throws<ArgumentOutOfRangeException>(()=>new EhlersAnticipateIndicatorSpecOptions(length,1,kind));
            Assert.Throws<ArgumentOutOfRangeException>(()=>new EhlersAnticipateIndicatorState(kind,length,1));
            Assert.Throws<ArgumentOutOfRangeException>(()=>new AnticipateWindow(kind,length,1));
            var data=Data(); data.SetCustomValues(new List<double>{17});
            Assert.Throws<ArgumentOutOfRangeException>(()=>data.CalculateEhlersAnticipateIndicator(kind,length,1));
            Assert.Equal(new[]{17d},data.CustomValuesList);
            using var context=new ComputeContext();
            Assert.Throws<ArgumentOutOfRangeException>(()=>IndicatorCompute.ComputeEhlersAnticipateIndicatorFast(data,context,length,kind,1));
            Assert.Equal(new[]{17d},data.CustomValuesList);
        }
    }
    [Fact]
    public void MaximumAndExistingMinimumNormalizationRemainAccepted()
    {
        Assert.Equal(4096,new EhlersAnticipateIndicatorSpecOptions(4096).Length);
        foreach(var length in new[]{int.MinValue,0,1,4096})
        {
            using var state=new EhlersAnticipateIndicatorState(length:length);
            var data=Data().CalculateEhlersAnticipateIndicator(length:length); Assert.Equal(new[]{0d},data.CustomValuesList);
            using var context=new ComputeContext(); using var result=IndicatorCompute.ComputeEhlersAnticipateIndicatorFast(Data(),context,length:length); Assert.Equal(new[]{0d},result.ToArray());
        }
    }
}

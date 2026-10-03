using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
using Band = OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.ChannelBand;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class HeadleyBandNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static OhlcvBar Native(Bar b)=>new("HEADLEY",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    [Fact]
    public void ExactRangeRatiosAndExtendedBoundariesSurviveAveraging()
    {
        foreach(var length in new[] {1,3,7})foreach(var kind in new[] {MovingAvgType.SimpleMovingAverage,MovingAvgType.WeightedMovingAverage,MovingAvgType.ExponentialMovingAverage,MovingAvgType.WildersSmoothingMethod})foreach(var factor in new[] {-.001,0,.001,double.MaxValue})foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/16})
            Check(Enumerable.Range(0,14).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),0,(i%5+2)*scale,-(i%3+1)*scale,(i%7-3)*scale,1)).ToArray(),length,kind,factor);
        Check(Array.Empty<Bar>(),20,MovingAvgType.SimpleMovingAverage,.001);
        var extreme=new[] {new Bar(DateTime.UnixEpoch,0,double.MaxValue,double.MaxValue/2,double.MaxValue/2,1),new Bar(DateTime.UnixEpoch.AddMinutes(1),0,-double.MaxValue/2,-double.MaxValue,-double.MaxValue/2,1),new Bar(DateTime.UnixEpoch.AddMinutes(2),0,double.MaxValue,-double.MaxValue,0,1)};
        Check(extreme,2,MovingAvgType.ExponentialMovingAverage,.001);
        var nearZero=new[] {new Bar(DateTime.UnixEpoch,0,1,-double.BitDecrement(1),.5,1),new Bar(DateTime.UnixEpoch.AddMinutes(1),0,0,0,0,1)};Check(nearZero,2,MovingAvgType.SimpleMovingAverage,double.MaxValue);
        var simple=new[] {new Bar(DateTime.UnixEpoch,10,12,8,10,1)};var batch=Data(simple).CalculatePriceHeadleyAccelerationBands(length:1,factor:.125);Assert.Equal(1212,batch.OutputValues["UpperBand"][0]);Assert.Equal(-792,batch.OutputValues["LowerBand"][0]);Assert.Equal(10,batch.OutputValues["MiddleBand"][0]);
    }
    private static void Check(Bar[] bars,int length,MovingAvgType kind,double factor)
    {
        var code=kind==MovingAvgType.SimpleMovingAverage?1:kind==MovingAvgType.WeightedMovingAverage?2:kind==MovingAvgType.ExponentialMovingAverage?3:6;
        var expected=BuiltInFormulaReferences.HeadleyBandOutputs(bars,length,code,factor);var batch=Data(bars).CalculatePriceHeadleyAccelerationBands(kind,length,factor);using var context=new ComputeContext();
        foreach(var key in expected.Keys) {Assert.Equal(expected[key],batch.OutputValues[key]);using var raw=IndicatorCompute.ComputePriceHeadleyAccelerationBandsFast(Data(bars),context,length,factor,kind,key=="UpperBand"?Band.Upper:key=="LowerBand"?Band.Lower:Band.Middle);Assert.Equal(expected[key],raw.Span.ToArray());}
        using var state=new PriceHeadleyAccelerationBandsState(kind,length,factor);for(var replay=0;replay<2;replay++) {state.Reset();for(var i=0;i<bars.Length;i++) {state.Update(Native(new Bar(bars[i].Time,3,4,-2,-1,7)),false,true);foreach(var final in new[] {false,false,true}) {var actual=state.Update(Native(bars[i]),final,true);foreach(var key in expected.Keys)Assert.Equal(expected[key][i],actual.Outputs![key]);}}}
    }
    [Fact]
    public void CustomerStagesConsumeSelectedCenterThenUpperThenLower()
    {
        var bars=Enumerable.Range(0,3).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),10,12,8,10,1)).ToArray();var prices=new[] {9d,10,11};using var context=new ComputeContext();
        foreach(var batch in new[] {false,true})
        {
            var data=Data(bars);data.SetCustomValues(prices.ToList());using var armed=ComponentAverage.Arm(new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>[] {
                (values,period)=>{Assert.Equal(3,period);Assert.Equal(prices,values);return new[] {1d,1,1};},
                (values,period)=>{Assert.Equal(3,period);Assert.Equal(new[] {1212d,1212,1212},values);return new[] {7d,7,7};},
                (values,period)=>{Assert.Equal(3,period);Assert.Equal(new[] {-792d,-792,-792},values);return new[] {-7d,-7,-7};}});
            if(batch) {var output=data.CalculatePriceHeadleyAccelerationBands(length:3,factor:.125);Assert.Equal(new[] {7d,7,7},output.OutputValues["UpperBand"]);Assert.Equal(new[] {-7d,-7,-7},output.OutputValues["LowerBand"]);}
            else {using var raw=IndicatorCompute.ComputePriceHeadleyAccelerationBandsFast(data,context,3,.125,band:Band.Upper);Assert.Equal(new[] {7d,7,7},raw.Span.ToArray());}Assert.Equal(3,ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidInputsNeverAdvanceNativeState()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            using var state=new PriceHeadleyAccelerationBandsState(length:2);using var control=new PriceHeadleyAccelerationBandsState(length:2);var first=Native(new Bar(DateTime.UnixEpoch,2,3,1,2,1));state.Update(first,true,true);control.Update(first,true,true);
            var values=new[] {2d,4,1,2,1};values[field]=invalid;var bad=new OhlcvBar("HEADLEY",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,values[0],values[1],values[2],values[3],values[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));var next=Native(new Bar(DateTime.UnixEpoch.AddMinutes(1),4,5,3,4,1));Assert.Equal(control.Update(next,true,true).Outputs!,state.Update(next,true,true).Outputs!);
        }
    }
    [Fact]
    public void LegacyBatchOnlyAveragesRetainEachBoundary()
    {
        var bars=Enumerable.Range(0,12).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),10,12,8,9+i%3,1)).ToArray();var kind=MovingAvgType.HullMovingAverage;
        var expected=CalculationsHelper.GetMovingAverageList(Data(bars),kind,3,Enumerable.Repeat(1212d,bars.Length).ToList());var batch=Data(bars).CalculatePriceHeadleyAccelerationBands(kind,3,.125);using var context=new ComputeContext();using var raw=IndicatorCompute.ComputePriceHeadleyAccelerationBandsFast(Data(bars),context,3,.125,kind,Band.Upper);Assert.Equal(expected,batch.OutputValues["UpperBand"]);Assert.Equal(expected,raw.Span.ToArray());
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(PriceHeadleyAccelerationBands)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.HeadleyBandOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}

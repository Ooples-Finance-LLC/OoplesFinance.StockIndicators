using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
using Band = OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.ChannelBand;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class TrendTraderNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static OhlcvBar Native(Bar b)=>new("TREND",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    [Fact]
    public void PreviousCloseExtremaAndExtendedAtrLimitsRetainStrictStopTies()
    {
        foreach(var length in new[] {0,1,2,5})foreach(var kind in new[] {MovingAvgType.SimpleMovingAverage,MovingAvgType.WeightedMovingAverage,MovingAvgType.ExponentialMovingAverage,MovingAvgType.WildersSmoothingMethod})foreach(var mult in new[] {-2d,0,1,3,double.MaxValue})foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/16})
            Check(Enumerable.Range(0,12).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),0,(i%5+2)*scale,-(i%3+1)*scale,(i%7-3)*scale,1)).ToArray(),length,kind,mult,2*scale);
        Check(Array.Empty<Bar>(),21,MovingAvgType.WeightedMovingAverage,3,20);
        Check(Enumerable.Range(0,8).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),double.MaxValue,double.MaxValue,double.MaxValue,double.MaxValue,1)).ToArray(),2,MovingAvgType.ExponentialMovingAverage,3,double.MaxValue);
        Check(new[] {new Bar(DateTime.UnixEpoch,-double.MaxValue,double.MaxValue,-double.MaxValue,-double.MaxValue,1),new Bar(DateTime.UnixEpoch.AddMinutes(1),1,1,1,1,1),new Bar(DateTime.UnixEpoch.AddMinutes(2),0,0,0,0,1)},2,MovingAvgType.WeightedMovingAverage,1,0);
        var extended=new[] {new Bar(DateTime.UnixEpoch,-double.MaxValue,double.MaxValue,-double.MaxValue,-double.MaxValue,1),new Bar(DateTime.UnixEpoch.AddMinutes(1),double.MaxValue,double.MaxValue,double.MaxValue,double.MaxValue,1)};
        Check(extended,3,MovingAvgType.WeightedMovingAverage,1,0);Assert.Equal(-double.MaxValue,Data(extended).CalculateTrendTraderBands(length:3,mult:1,bandStep:0).OutputValues["MiddleBand"][1]);
        foreach(var sign in new[] {-1d,1d})Check(new[] {10d,13,15,10,8,13}.Select((v,i)=>new Bar(DateTime.UnixEpoch.AddMinutes(i),sign*v,sign*v+1,sign*v-1,sign*v,1)).ToArray(),1,MovingAvgType.WeightedMovingAverage,1,-2);
        var bars=new[] {10d,13,15,10}.Select((v,i)=>new Bar(DateTime.UnixEpoch.AddMinutes(i),v,v+1,v-1,v,1)).ToArray();var batch=Data(bars).CalculateTrendTraderBands(length:1,mult:1,bandStep:2);
        Assert.Equal(new[] {0d,8,8,18},batch.OutputValues["MiddleBand"]);Assert.Equal(new[] {2d,10,10,20},batch.OutputValues["UpperBand"]);Assert.Equal(new[] {-2d,6,6,16},batch.OutputValues["LowerBand"]);
    }
    private static void Check(Bar[] bars,int length,MovingAvgType kind,double mult,double step)
    {
        var code=kind==MovingAvgType.SimpleMovingAverage?1:kind==MovingAvgType.WeightedMovingAverage?2:kind==MovingAvgType.ExponentialMovingAverage?3:6;
        var expected=BuiltInFormulaReferences.TrendTraderOutputs(bars,length,code,mult,step);var batch=Data(bars).CalculateTrendTraderBands(kind,length,mult,step);using var context=new ComputeContext();
        foreach(var key in new[] {"UpperBand","MiddleBand","LowerBand"}) {Assert.Equal(expected[key],batch.OutputValues[key]);using var raw=IndicatorCompute.ComputeTrendTraderBandsFast(Data(bars),context,length,mult,step,kind,key=="UpperBand"?Band.Upper:key=="LowerBand"?Band.Lower:Band.Middle);Assert.Equal(expected[key],raw.Span.ToArray());}
        using var state=new TrendTraderBandsState(kind,length,mult,step);for(var replay=0;replay<2;replay++) {state.Reset();for(var i=0;i<bars.Length;i++) {state.Update(Native(new Bar(bars[i].Time,3,4,-2,-1,7)),false,true);foreach(var final in new[] {false,false,true}) {var actual=state.Update(Native(bars[i]),final,true);foreach(var key in new[] {"UpperBand","MiddleBand","LowerBand"})Assert.Equal(expected[key][i],actual.Outputs![key]);}}}
    }
    [Fact]
    public void SelectedCloseAndOrderedCustomerAtrStopStagesDriveBothStrictTies()
    {
        using var context=new ComputeContext();foreach(var prices in new[] {new[] {10d,12,15,13,10},new[] {10d,8,5,7,10}})foreach(var external in new[] {false,true})foreach(var batch in new[] {false,true})
        {
            var bars=prices.Select((v,i)=>new Bar(DateTime.UnixEpoch.AddMinutes(i),4,v+1,v-1,4,1)).ToArray();var selected=bars.Select((b,i)=>new Bar(b.Time,b.Open,b.High,b.Low,prices[i],b.Volume)).ToArray();var data=Data(bars);data.SetCustomValues(prices.ToList());var atr=Enumerable.Repeat(2d,prices.Length).ToArray();var average=new[] {1d,2,3,4,5};var expected=BuiltInFormulaReferences.TrendTraderOutputs(selected,1,2,1,2,external?atr:null,external?average:null);var ranges=new[] {2d,3,4,3,4};
            using var armed=external?ComponentAverage.Arm(new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>[] {(values,period)=>{Assert.Equal(1,period);Assert.Equal(ranges,values);return atr;},(values,period)=>{Assert.Equal(1,period);Assert.Equal(expected["Raw"],values);return average;}}):null;
            if(batch) {var result=data.CalculateTrendTraderBands(length:1,mult:1,bandStep:2);Assert.Equal(expected["MiddleBand"],result.OutputValues["MiddleBand"]);}
            else {using var raw=IndicatorCompute.ComputeTrendTraderBandsFast(data,context,1,1,2);Assert.Equal(expected["MiddleBand"],raw.Span.ToArray());}if(external)Assert.Equal(2,ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidInputsAndCoefficientsNeverAdvanceNativeState()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            using var state=new TrendTraderBandsState(length:2);using var control=new TrendTraderBandsState(length:2);var first=Native(new Bar(DateTime.UnixEpoch,2,3,1,2,1));state.Update(first,true,true);control.Update(first,true,true);
            var values=new[] {2d,4,1,2,1};values[field]=invalid;var bad=new OhlcvBar("TREND",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,values[0],values[1],values[2],values[3],values[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));var next=Native(new Bar(DateTime.UnixEpoch.AddMinutes(1),4,5,3,4,1));Assert.Equal(control.Update(next,true,true).Outputs!,state.Update(next,true,true).Outputs!);
        }
        foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity}) {Assert.Throws<ArgumentOutOfRangeException>(()=>new TrendTraderBandsState(mult:invalid));Assert.Throws<ArgumentOutOfRangeException>(()=>new TrendTraderBandsState(bandStep:invalid));Assert.Throws<ArgumentOutOfRangeException>(()=>Data(Array.Empty<Bar>()).CalculateTrendTraderBands(mult:invalid));using var context=new ComputeContext();Assert.Throws<ArgumentOutOfRangeException>(()=>IndicatorCompute.ComputeTrendTraderBandsFast(Data(Array.Empty<Bar>()),context,bandStep:invalid));}
    }
    [Fact]
    public void LegacyBatchAveragesKeepAtrAndStopStages()
    {
        var bars=new[] {2d,7,4,9,3,5}.Select((v,i)=>new Bar(DateTime.UnixEpoch.AddMinutes(i),v,v+1,v-1,v,1)).ToArray();var kind=MovingAvgType.HullMovingAverage;var atr=Data(bars).CalculateAverageTrueRange(kind,3).CustomValuesList.ToArray();var raw=BuiltInFormulaReferences.TrendTraderOutputs(bars,3,1,1,2,atr)["Raw"];var middle=CalculationsHelper.GetMovingAverageList(Data(bars),kind,3,raw.ToList());var batch=Data(bars).CalculateTrendTraderBands(kind,3,1,2);Assert.Equal(middle,batch.OutputValues["MiddleBand"]);using var context=new ComputeContext();using var actual=IndicatorCompute.ComputeTrendTraderBandsFast(Data(bars),context,3,1,2,kind);Assert.Equal(middle,actual.Span.ToArray());
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(TrendTraderBands)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.TrendTraderOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}

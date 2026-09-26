using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class RocBandNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static Bar[] Bars(double[] prices)=>prices.Select((v,i)=>new Bar(DateTime.UnixEpoch.AddMinutes(i),v,v,v,v,1)).ToArray();
    private static OhlcvBar Native(Bar b)=>new("ROCB",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    [Fact]
    public void ExtendedReturnsAndExactRmsPreserveStartupEvictionAndZeroCenter()
    {
        foreach(var length in new[] {1,3,7})foreach(var smooth in new[] {1,2,5})foreach(var kind in new[] {MovingAvgType.SimpleMovingAverage,MovingAvgType.WeightedMovingAverage,MovingAvgType.ExponentialMovingAverage,MovingAvgType.WildersSmoothingMethod})foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/16})
            Check(Bars(Enumerable.Range(0,16).Select(i=>(i%7-3)*scale).ToArray()),length,smooth,kind);
        Check(Array.Empty<Bar>(),12,3,MovingAvgType.ExponentialMovingAverage);
        var extremes=Bars(new[] {double.Epsilon,double.MaxValue,-double.MaxValue,0,1,2,4,8,16,32});Check(extremes,1,3,MovingAvgType.SimpleMovingAverage);
        var overflow=Data(extremes).CalculateRateOfChangeBands(length:1,smoothLength:3);Assert.Equal(double.PositiveInfinity,overflow.OutputValues["UpperBand"][1]);Assert.Equal(100,overflow.OutputValues["UpperBand"][7]);Assert.All(overflow.OutputValues["MiddleBand"],v=>Assert.Equal(0,v));
        var simple=Data(Bars(new[] {1d,2,4})).CalculateRateOfChangeBands(length:1,smoothLength:1);Assert.Equal(new[] {0d,100,100},simple.OutputValues["UpperBand"]);Assert.Equal(new[] {0d,-100,-100},simple.OutputValues["LowerBand"]);Assert.Equal(new[] {0d,100,100},simple.OutputValues["Roc"]);
    }
    private static void Check(Bar[] bars,int length,int smooth,MovingAvgType kind)
    {
        var code=kind==MovingAvgType.SimpleMovingAverage?1:kind==MovingAvgType.WeightedMovingAverage?2:kind==MovingAvgType.ExponentialMovingAverage?3:6;
        var expected=BuiltInFormulaReferences.RocBandOutputs(bars,length,smooth,code);var batch=Data(bars).CalculateRateOfChangeBands(kind,length,smooth);using var context=new ComputeContext();
        foreach(var key in expected.Keys) {Assert.Equal(expected[key],batch.OutputValues[key]);using var raw=IndicatorCompute.ComputeRateOfChangeBandsFast(Data(bars),context,length,smooth,kind,key);Assert.Equal(expected[key],raw.Span.ToArray());}
        using var state=new RateOfChangeBandsState(kind,length,smooth);for(var replay=0;replay<2;replay++) {state.Reset();for(var i=0;i<bars.Length;i++) {state.Update(Native(new Bar(bars[i].Time,3,4,-2,-1,7)),false,true);foreach(var final in new[] {false,false,true}) {var actual=state.Update(Native(bars[i]),final,true);Assert.Equal(expected["Roc"][i],actual.Value);foreach(var key in expected.Keys)Assert.Equal(expected[key][i],actual.Outputs![key]);}}}
    }
    [Fact]
    public void CustomerSmoothingReceivesSelectedReturnsAndItsOwnPeriod()
    {
        var bars=Bars(new[] {4d,4,4,4});var prices=new[] {1d,2,4,8};using var context=new ComputeContext();
        foreach(var batch in new[] {false,true})
        {
            var data=Data(bars);data.SetCustomValues(prices.ToList());using var armed=ComponentAverage.Arm(new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>[] {
                (values,period)=>{Assert.Equal(3,period);Assert.Equal(new[] {0d,0,300,300},values);return new[] {7d,7,7,7};}});
            if(batch)Assert.Equal(new[] {7d,7,7,7},data.CalculateRateOfChangeBands(length:2,smoothLength:3).OutputValues["Roc"]);
            else {using var raw=IndicatorCompute.ComputeRateOfChangeBandsFast(data,context,2,3,outputKey:"Roc");Assert.Equal(new[] {7d,7,7,7},raw.Span.ToArray());}Assert.Equal(1,ComponentAverage.Substitutions);
        }
        var selected=Data(bars);selected.SetCustomValues(prices.ToList());using var result=IndicatorCompute.ComputeRateOfChangeBandsFast(selected,context,2,3,outputKey:"UpperBand");Assert.Equal(BuiltInFormulaReferences.RocBandOutputs(Bars(prices),2,3,3)["UpperBand"],result.Span.ToArray());
    }
    [Fact]
    public void InvalidInputsNeverAdvanceNativeState()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            using var state=new RateOfChangeBandsState(length:1,smoothLength:2);using var control=new RateOfChangeBandsState(length:1,smoothLength:2);var first=Native(new Bar(DateTime.UnixEpoch,2,3,1,2,1));state.Update(first,true,true);control.Update(first,true,true);
            var values=new[] {2d,4,1,2,1};values[field]=invalid;var bad=new OhlcvBar("ROCB",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,values[0],values[1],values[2],values[3],values[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));var next=Native(new Bar(DateTime.UnixEpoch.AddMinutes(1),4,5,3,4,1));Assert.Equal(control.Update(next,true,true).Outputs!,state.Update(next,true,true).Outputs!);
        }
    }
    [Fact]
    public void LegacyBatchOnlyAveragesStillSmoothReturns()
    {
        var prices=Enumerable.Range(0,12).Select(i=>Math.Pow(2,i)).ToArray();var bars=Bars(prices);var kind=MovingAvgType.HullMovingAverage;
        var expected=CalculationsHelper.GetMovingAverageList(Data(bars),kind,3,Enumerable.Range(0,12).Select(i=>i<2?0d:300).ToList());var batch=Data(bars).CalculateRateOfChangeBands(kind,2,3);using var context=new ComputeContext();using var raw=IndicatorCompute.ComputeRateOfChangeBandsFast(Data(bars),context,2,3,kind,"Roc");Assert.Equal(expected,batch.OutputValues["Roc"]);Assert.Equal(expected,raw.Span.ToArray());
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(RateOfChangeBands)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.RocBandOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}

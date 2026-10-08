using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class MotionAttractionNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static Bar[] Bars(double[] prices)=>prices.Select((v,i)=>new Bar(DateTime.UnixEpoch.AddMinutes(i),v,v,v,v,1)).ToArray();
    private static OhlcvBar Native(Bar b)=>new("MTA",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    [Fact]
    public void ExactAttractionStepsSaturateAndStopsKeepStrictPreviousBoundaryTies()
    {
        foreach(var length in new[] {0,1,2,3,10,14,int.MaxValue})foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/8})
            Check(Bars(Enumerable.Range(0,36).Select(i=>(i%11-5)*scale).ToArray()),length);
        Check(Array.Empty<Bar>(),14);Check(Bars(new[] {double.MaxValue,-double.MaxValue,double.MaxValue,0d}),3);
        Check(Bars(Enumerable.Repeat(double.MaxValue,8).ToArray()),3);Check(Bars(Enumerable.Range(0,25).Select(i=>(double)i).ToArray()),10);Check(Bars(Enumerable.Range(0,25).Select(i=>-(double)i).ToArray()),10);
        Check(Bars(new[] {1d,2,3,0,2.25,0}),2);Check(Bars(new[] {1d,2,3,2}),2);
        var bars=Bars(new[] {1d,2,3,0});var batch=Data(bars).CalculateMotionToAttractionChannels(2);
        Assert.Equal(new[] {1d,2,3,2.25},batch.OutputValues["UpperBand"]);Assert.Equal(new[] {1d,1.25,2,0},batch.OutputValues["LowerBand"]);Assert.Equal(new[] {1d,1.625,2.5,1.125},batch.OutputValues["MiddleBand"]);
        Assert.Equal(new[] {1d,1.25,2,2.25},Data(bars).CalculateMotionToAttractionTrailingStop(2).CustomValuesList);
    }
    private static void Check(Bar[] bars,int length)
    {
        var expected=BuiltInFormulaReferences.MotionAttractionOutputs(bars,length);var batch=Data(bars).CalculateMotionToAttractionChannels(length);var stop=Data(bars).CalculateMotionToAttractionTrailingStop(length);using var context=new ComputeContext();
        foreach(var key in expected.Keys) {Assert.Equal(expected[key],key=="Ts"?stop.CustomValuesList:batch.OutputValues[key]);using var raw=IndicatorCompute.ComputeMotionAttractionFast(Data(bars),context,length,key);Assert.Equal(expected[key],raw.Span.ToArray());}
        var state=new MotionToAttractionChannelsState(length);var stopState=new MotionToAttractionTrailingStopState(length);
        for(var replay=0;replay<2;replay++) {state.Reset();stopState.Reset();for(var i=0;i<bars.Length;i++) {var alternate=Native(new Bar(bars[i].Time,3,4,-2,-1,7));state.Update(alternate,false,true);stopState.Update(alternate,false,true);foreach(var final in new[] {false,false,true}) {var actual=state.Update(Native(bars[i]),final,true);var actualStop=stopState.Update(Native(bars[i]),final,true);foreach(var key in expected.Keys)Assert.Equal(expected[key][i],key=="Ts"?actualStop.Value:actual.Outputs![key]);Assert.Equal(actualStop.Value,actualStop.Outputs!["Ts"]);Assert.InRange(actual.Value,actual.Outputs!["LowerBand"],actual.Outputs["UpperBand"]);}}}
    }
    [Fact]
    public void SelectedClosesDriveBothChannelsAndStopDirection()
    {
        var prices=new[] {1d,2,3,0,2.25};var data=Data(Bars(new[] {4d,4,4,4,4}));data.SetCustomValues(prices.ToList());var expected=BuiltInFormulaReferences.MotionAttractionOutputs(Bars(prices),2);using var context=new ComputeContext();
        using var raw=IndicatorCompute.ComputeMotionAttractionFast(data,context,2,"Ts");Assert.Equal(expected["Ts"],raw.Span.ToArray());Assert.Equal(expected["UpperBand"],data.CalculateMotionToAttractionChannels(2).OutputValues["UpperBand"]);
        data=Data(Bars(new[] {4d,4,4,4,4}));data.SetCustomValues(prices.ToList());Assert.Equal(expected["Ts"],data.CalculateMotionToAttractionTrailingStop(2).CustomValuesList);
    }
    [Fact]
    public void InvalidInputsNeverAdvanceEitherNativeState()
    {
        foreach(var stop in new[] {false,true})foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            IStreamingIndicatorState state=stop?new MotionToAttractionTrailingStopState(2):new MotionToAttractionChannelsState(2);IStreamingIndicatorState control=stop?new MotionToAttractionTrailingStopState(2):new MotionToAttractionChannelsState(2);
            var first=Native(new Bar(DateTime.UnixEpoch,2,3,1,2,1));state.Update(first,true,true);control.Update(first,true,true);
            var values=new[] {2d,4,1,2,1};values[field]=invalid;var bad=new OhlcvBar("MTA",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,values[0],values[1],values[2],values[3],values[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));var next=Native(new Bar(DateTime.UnixEpoch.AddMinutes(1),4,5,3,4,1));Assert.Equal(control.Update(next,true,true).Outputs!,state.Update(next,true,true).Outputs!);
        }
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(MotionToAttractionChannels)||c.IndicatorType==typeof(MotionToAttractionTrailingStop)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.MotionAttractionOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}

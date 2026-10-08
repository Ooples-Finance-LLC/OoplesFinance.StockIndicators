using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class ProjectedLevelsNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static OhlcvBar Native(Bar b)=>new("TIRONE",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    private static Bar Candle(int i,double high,double low,double close)=>new(DateTime.UnixEpoch.AddMinutes(i),0,high,low,close,1);
    [Fact]
    public void RollingLevelsMatchIndependentFractionsIncludingExtremeRanges()
    {
        foreach(var length in new[]{0,1,2,5,int.MaxValue})
        {
            foreach(var scale in new[]{1d,double.Epsilon,double.MaxValue/16})Check(Enumerable.Range(0,18).Select(i=>Candle(i,(i%7+2)*scale,-(i%5+1)*scale,(i%5-2)*scale)).ToArray(),length);
            Check(Array.Empty<Bar>(),length);
            Check(Enumerable.Range(0,9).Select(i=>Candle(i,double.MaxValue,-double.MaxValue,i%2==0?double.MaxValue:-double.MaxValue)).ToArray(),length);
            var flat=Enumerable.Range(0,5).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),double.MaxValue,double.MaxValue,double.MaxValue,double.MaxValue,1)).ToArray();Check(flat,length);
            foreach(var values in BuiltInFormulaReferences.ProjectedLevelsOutputs(flat,length).Values)Assert.All(values,v=>Assert.Equal(double.MaxValue,v));
        }
        var hand=new[]{Candle(0,10,1,7)};Check(hand,2);
        Assert.Equal(new[]{-1.25,-3.5,12.25,14.5,5.5},BuiltInFormulaReferences.ProjectedLevelsOutputs(hand,2).Values.Select(v=>v[0]));
    }
    private static void Check(Bar[] bars,int length)
    {
        var expected=BuiltInFormulaReferences.ProjectedLevelsOutputs(bars,length);var batch=Data(bars).CalculateProjectedSupportAndResistance(length);
        foreach(var key in expected.Keys)
        {
            Assert.Equal(expected[key],batch.OutputValues[key]);using var context=new ComputeContext();using var result=IndicatorCompute.ComputeArm(Data(bars),new IndicatorSpec(IndicatorName.ProjectedSupportAndResistance,new ProjectedSupportAndResistanceSpecOptions(length),key),context);Assert.NotNull(result);Assert.Equal(expected[key],result.Value.ToArray());
        }
        using var state=new ProjectedSupportAndResistanceState(length);
        for(var replay=0;replay<2;replay++){state.Reset();for(var i=0;i<bars.Length;i++)
        {
            state.Update(Native(Candle(i,100,-100,50)),false,true);
            foreach(var final in new[]{false,false,true}){var actual=state.Update(Native(bars[i]),final,true);Assert.Equal(expected["MiddleBand"][i],actual.Value);foreach(var key in expected.Keys)Assert.Equal(expected[key][i],actual.Outputs![key]);}
        }}
    }
    [Fact]
    public void SelectedPricesKeepOriginalRollingExtrema()
    {
        var bars=Enumerable.Range(0,12).Select(i=>Candle(i,i%5+3,-(i%3+1),1)).ToArray();var selected=Enumerable.Range(0,12).Select(i=>i%2==0?100d:-50d).ToArray();var expected=BuiltInFormulaReferences.ProjectedLevelsOutputs(bars.Select((b,i)=>new Bar(b.Time,b.Open,b.High,b.Low,selected[i],b.Volume)).ToArray(),3);
        foreach(var key in expected.Keys)foreach(var route in new[]{"batch","fast","arm"})
        {
            var data=Data(bars);data.SetCustomValues(selected.ToList());if(route=="batch"){Assert.Equal(expected[key],data.CalculateProjectedSupportAndResistance(3).OutputValues[key]);continue;}
            using var context=new ComputeContext();var spec=new IndicatorSpec(IndicatorName.ProjectedSupportAndResistance,new ProjectedSupportAndResistanceSpecOptions(3),key);using var result=route=="arm"?IndicatorCompute.ComputeArm(data,spec,context):IndicatorCompute.TryComputeFast(data,spec,context);Assert.NotNull(result);Assert.Equal(expected[key],result.Value.ToArray());
        }
    }
    [Fact]
    public void InvalidFieldsDoNotCommitExtrema()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[]{false,true})
        {
            using var state=new ProjectedSupportAndResistanceState(3);using var control=new ProjectedSupportAndResistanceState(3);var first=Native(Candle(0,3,0,1));state.Update(first,true,true);control.Update(first,true,true);var v=new[]{0d,4,-2,1,1};v[field]=invalid;var bad=new OhlcvBar("TIRONE",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,v[0],v[1],v[2],v[3],v[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));foreach(var i in Enumerable.Range(1,9)){var bar=Native(Candle(i,i+1,-2,i%3));Assert.Equal(control.Update(bar,true,true).Outputs!,state.Update(bar,true,true).Outputs!);}
        }
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[]{typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(ProjectedSupportAndResistance)).Select(c=>new object[]{c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[]{"batch","fast","arm","native","streaming"}.Select(route=>new object[]{c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.ProjectedLevelsOutputs(bars,((ProjectedSupportAndResistanceSpecOptions)((IBuiltInIndicator)testCase.Factory()).CreateOptions()).Length),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}

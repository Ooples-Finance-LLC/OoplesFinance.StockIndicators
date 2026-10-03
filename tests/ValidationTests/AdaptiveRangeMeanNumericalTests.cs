using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class AdaptiveRangeMeanNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static OhlcvBar Native(Bar b)=>new("CMF",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    private static Bar Candle(int i,double h,double l,double c,double v)=>new(DateTime.UnixEpoch.AddMinutes(i),c,h,l,c,v);
    [Fact]
    public void RangeGainAndConvexRecursionMatchIndependentFractions()
    {
        foreach(var length in new[] {int.MinValue,0,1,2,14,int.MaxValue})
        foreach(var pair in new[] {(0,1),(2,14),(14,2),(int.MaxValue,14),(2,int.MaxValue)})
        {
            foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/8})
                Check(Enumerable.Range(0,36).Select(i=>Candle(i,(i%5+1)*scale,-(i%3+1)*scale,(i%7-3)*scale,1)).ToArray(),length,pair.Item1,pair.Item2);
            Check(new[] {Candle(0,double.MaxValue,-double.MaxValue,double.MaxValue,1),Candle(1,double.MaxValue,-double.MaxValue,-double.MaxValue,1),Candle(2,double.MaxValue,double.MaxValue,double.MaxValue,1)},length,pair.Item1,pair.Item2);
        }
        Check(Array.Empty<Bar>(),14,2,14);
        var hand=new[] {Candle(0,2,0,1,1),Candle(1,2,0,2,1),Candle(2,2,0,0,1)};
        Assert.Equal(new[] {.25,2d,0d},BuiltInFormulaReferences.AdaptiveRangeMeanOutputs(hand,2,1,3)["Ama"]);Check(hand,2,1,3);
        Check(Enumerable.Range(0,12).Select(i=>Candle(i,4,4,4,1)).ToArray(),2,2,14);
    }
    private static void Check(Bar[] bars,int length,int fast,int slow)
    {
        var expected=BuiltInFormulaReferences.AdaptiveRangeMeanOutputs(bars,length,fast,slow)["Ama"];
        Assert.Equal(expected,Data(bars).CalculateAdaptiveMovingAverage(fast,slow,length).ChainedValues);
        using var context=new ComputeContext();using var raw=IndicatorCompute.ComputeAmaFast(Data(bars),context,fast,slow,length);Assert.Equal(expected,raw.Span.ToArray());
        using var state=new AdaptiveMovingAverageState(fast,slow,length);
        for(var replay=0;replay<2;replay++){state.Reset();for(var i=0;i<bars.Length;i++){state.Update(Native(Candle(i,40,-30,20,7)),false,true);foreach(var final in new[] {false,false,true}){var result=state.Update(Native(bars[i]),final,true);Assert.Equal(expected[i],result.Value);Assert.Equal(expected[i],result.Outputs!["Ama"]);}}}
    }
    [Fact]
    public void SelectedPricesKeepOriginalRange()
    {
        var original=Enumerable.Range(0,12).Select(i=>Candle(i,8,-2,1,i+1)).ToArray();var selected=Enumerable.Range(0,12).Select(i=>(double)(i%11-3)).ToArray();
        var expected=BuiltInFormulaReferences.AdaptiveRangeMeanOutputs(original.Select((b,i)=>new Bar(b.Time,b.Open,b.High,b.Low,selected[i],b.Volume)).ToArray(),3,2,7)["Ama"];
        var data=Data(original);data.SetCustomValues(selected.ToList());using var context=new ComputeContext();using var raw=IndicatorCompute.ComputeAmaFast(data,context,2,7,3);Assert.Equal(expected,raw.Span.ToArray());Assert.Equal(expected,data.CalculateAdaptiveMovingAverage(2,7,3).ChainedValues);
    }
    [Fact]
    public void InvalidFieldsCannotAdvanceExtremaOrRecursion()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            using var state=new AdaptiveMovingAverageState(length:3);using var control=new AdaptiveMovingAverageState(length:3);var first=Native(Candle(0,3,0,1,2));state.Update(first,true,true);control.Update(first,true,true);var v=new[] {0d,4,-2,1,1};v[field]=invalid;var bad=new OhlcvBar("AMA",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,v[0],v[1],v[2],v[3],v[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));foreach(var i in Enumerable.Range(1,6)){var bar=Native(Candle(i,4,-2,i%5-1,i+1));Assert.Equal(control.Update(bar,true,true).Outputs!,state.Update(bar,true,true).Outputs!);}
        }
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(Ama)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.AdaptiveRangeMeanOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}

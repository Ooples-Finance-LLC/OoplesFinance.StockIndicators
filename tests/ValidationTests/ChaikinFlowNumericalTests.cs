using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class ChaikinFlowNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static OhlcvBar Native(Bar b)=>new("CMF",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    private static Bar Candle(int i,double h,double l,double c,double v)=>new(DateTime.UnixEpoch.AddMinutes(i),c,h,l,c,v);
    [Fact]
    public void RollingFlowAndVolumeMatchIndependentFractions()
    {
        foreach(var length in new[] {0,1,2,3,20,int.MaxValue})
        {
            foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/8})
                Check(Enumerable.Range(0,48).Select(i=>Candle(i,3*scale,-3*scale,(i%7-3)*scale,(i%5)*scale)).ToArray(),length);
            Check(Array.Empty<Bar>(),length);
            Check(new[] {Candle(0,double.MaxValue,-double.MaxValue,double.MaxValue,double.MaxValue),Candle(1,double.MaxValue,-double.MaxValue,-double.MaxValue,double.MaxValue),Candle(2,2,0,1,0),Candle(3,0,0,0,7)},length);
            // Selected closes can lie outside the original range. Keep overflowed flow unpublished until the final ratio.
            Check(new[] {Candle(0,double.Epsilon,0,double.MaxValue,double.MaxValue),Candle(1,double.Epsilon,0,-double.MaxValue,double.MaxValue),Candle(2,2,0,1,1)},length);
        }
        var hand=new[] {Candle(0,2,0,2,2),Candle(1,2,0,0,1),Candle(2,2,0,1,1)};
        Assert.Equal(new[] {1d,1d/3,-.5},BuiltInFormulaReferences.ChaikinFlowOutputs(hand,2)["Cmf"]);Check(hand,2);
    }
    private static void Check(Bar[] bars,int length)
    {
        var expected=BuiltInFormulaReferences.ChaikinFlowOutputs(bars,length)["Cmf"];
        Assert.Equal(expected,Data(bars).CalculateChaikinMoneyFlow(length).ChainedValues);
        using var context=new ComputeContext();using var raw=IndicatorCompute.ComputeCmfFast(Data(bars),context,length);Assert.Equal(expected,raw.Span.ToArray());
        var core=new double[bars.Length];OoplesFinance.StockIndicators.Core.VolumeCore.ChaikinMoneyFlow(bars.Select(b=>b.High).ToArray(),bars.Select(b=>b.Low).ToArray(),bars.Select(b=>b.Close).ToArray(),bars.Select(b=>b.Volume).ToArray(),core,length);Assert.Equal(expected,core);
        using var state=new ChaikinMoneyFlowState(length);
        for(var replay=0;replay<2;replay++){state.Reset();for(var i=0;i<bars.Length;i++){state.Update(Native(Candle(i,4,-3,2,7)),false,true);foreach(var final in new[] {false,false,true}){var result=state.Update(Native(bars[i]),final,true);Assert.Equal(expected[i],result.Value);Assert.Equal(expected[i],result.Outputs!["Cmf"]);}}}
    }
    [Fact]
    public void SelectedPricesKeepOriginalRangeAndVolume()
    {
        var original=Enumerable.Range(0,12).Select(i=>Candle(i,8,-2,1,i+1)).ToArray();var selected=Enumerable.Range(0,12).Select(i=>(double)(i*3-8)).ToArray();
        var expected=BuiltInFormulaReferences.ChaikinFlowOutputs(original.Select((b,i)=>new Bar(b.Time,b.Open,b.High,b.Low,selected[i],b.Volume)).ToArray(),3)["Cmf"];
        var data=Data(original);data.SetCustomValues(selected.ToList());using var context=new ComputeContext();using var raw=IndicatorCompute.ComputeCmfFast(data,context,3);Assert.Equal(expected,raw.Span.ToArray());Assert.Equal(expected,data.CalculateChaikinMoneyFlow(3).ChainedValues);
    }
    [Fact]
    public void InvalidFieldsCannotAdvanceFlowOrVolume()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            using var state=new ChaikinMoneyFlowState(3);using var control=new ChaikinMoneyFlowState(3);var first=Native(Candle(0,3,0,1,2));state.Update(first,true,true);control.Update(first,true,true);var v=new[] {0d,4,-2,1,1};v[field]=invalid;var bad=new OhlcvBar("CMF",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,v[0],v[1],v[2],v[3],v[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));foreach(var i in Enumerable.Range(1,6)){var bar=Native(Candle(i,4,-2,i%5-1,i+1));Assert.Equal(control.Update(bar,true,true).Outputs!,state.Update(bar,true,true).Outputs!);}
        }
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(Cmf)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.ChaikinFlowOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}

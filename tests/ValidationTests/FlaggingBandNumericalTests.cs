using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class FlaggingBandNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static Bar[] Bars(double[] prices)=>prices.Select((v,i)=>new Bar(DateTime.UnixEpoch.AddMinutes(i),v,v,v,v,1)).ToArray();
    private static OhlcvBar Native(Bar b)=>new("FLAG",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    [Fact]
    public void FlatBoundaryDecayAndTwoBarDirectionUseExactPopulationDeviation()
    {
        foreach(var length in new[] {0,1,2,3,14,100})foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/8})
            Check(Bars(Enumerable.Range(0,36).Select(i=>(i%11-5)*scale).ToArray()),length);
        Check(Bars(new[] {0d,2,1}),2); Check(Bars(new[] {0d,-2,-1}),2);
        Check(Array.Empty<Bar>(),14);Check(Bars(new[] {double.MaxValue,-double.MaxValue,double.MaxValue,0d,0,0}),2);
        Check(Bars(Enumerable.Repeat(double.MaxValue,8).ToArray()),3);Check(Bars(Enumerable.Repeat(-double.MaxValue,8).ToArray()),3);
        foreach(var sign in new[] {-1d,1d}) {Check(Bars(new[] {1d,3,2,2,0,0,0,2,2,1,1}.Select(v=>sign*v).ToArray()),2);Check(Bars(new[] {0d,2,0,0,2,0,-2,-2,0}),3);}
        var batch=Data(Bars(new[] {1d,3,2,2,0})).CalculateFlaggingBands(2);
        Assert.Equal(new[] {1d,3,3,3,2.5},batch.OutputValues["UpperBand"]);Assert.Equal(new[] {1d,1,1.25,1.25,0},batch.OutputValues["LowerBand"]);
        Assert.Equal(new[] {1d,1.5,2.5625,2.5625,.625},batch.OutputValues["MiddleBand"]);Assert.Equal(new[] {1d,3,1.25,1.25,2.5},batch.OutputValues["TrailingStop"]);
    }
    private static void Check(Bar[] bars,int length)
    {
        var expected=BuiltInFormulaReferences.FlaggingBandOutputs(bars,length);var batch=Data(bars).CalculateFlaggingBands(length);using var context=new ComputeContext();
        foreach(var key in expected.Keys) {Assert.Equal(expected[key],batch.OutputValues[key]);using var raw=IndicatorCompute.ComputeFlaggingBandsFast(Data(bars),context,length,key);Assert.Equal(expected[key],raw.Span.ToArray());}
        using var state=new FlaggingBandsState(length);
        for(var replay=0;replay<2;replay++) {state.Reset();for(var i=0;i<bars.Length;i++) {state.Update(Native(new Bar(bars[i].Time,3,4,-2,-1,7)),false,true);foreach(var final in new[] {false,false,true}) {var actual=state.Update(Native(bars[i]),final,true);foreach(var key in expected.Keys)Assert.Equal(expected[key][i],actual.Outputs![key]);Assert.InRange(bars[i].Close,actual.Outputs!["LowerBand"],actual.Outputs["UpperBand"]);Assert.InRange(actual.Value,actual.Outputs["LowerBand"],actual.Outputs["UpperBand"]);}}}
    }
    [Fact]
    public void SelectedCloseControlsDeviationAndDirection()
    {
        var prices=new[] {1d,3,2,2,0};var data=Data(Bars(new[] {4d,4,4,4,4}));data.SetCustomValues(prices.ToList());var expected=BuiltInFormulaReferences.FlaggingBandOutputs(Bars(prices),2);using var context=new ComputeContext();
        using var raw=IndicatorCompute.ComputeFlaggingBandsFast(data,context,2,"TrailingStop");Assert.Equal(expected["TrailingStop"],raw.Span.ToArray());var batch=data.CalculateFlaggingBands(2);foreach(var key in expected.Keys)Assert.Equal(expected[key],batch.OutputValues[key]);
    }
    [Fact]
    public void InvalidInputsNeverAdvanceNativeState()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            using var state=new FlaggingBandsState(2);using var control=new FlaggingBandsState(2);var first=Native(new Bar(DateTime.UnixEpoch,2,3,1,2,1));state.Update(first,true,true);control.Update(first,true,true);
            var values=new[] {2d,4,1,2,1};values[field]=invalid;var bad=new OhlcvBar("FLAG",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,values[0],values[1],values[2],values[3],values[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));var next=Native(new Bar(DateTime.UnixEpoch.AddMinutes(1),4,5,3,4,1));Assert.Equal(control.Update(next,true,true).Outputs!,state.Update(next,true,true).Outputs!);
        }
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(FlaggingBands)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.FlaggingBandOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}

using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class DEnvelopeNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static Bar[] Bars(double[] prices)=>prices.Select((v,i)=>new Bar(DateTime.UnixEpoch.AddMinutes(i),v,v,v,v,1)).ToArray();
    private static OhlcvBar Native(Bar b)=>new("DE",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    [Fact]
    public void ZeroSeededCorrectionAndDeviationClampRetainExtendedState()
    {
        foreach(var length in new[] {0,1,2,7,20,int.MaxValue})foreach(var factor in new[] {-2d,0,2,double.MaxValue})foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/8})
            Check(Bars(Enumerable.Range(0,20).Select(i=>(i%11-5)*scale).ToArray()),length,factor);
        Check(Array.Empty<Bar>(),20,2);Check(Bars(new[] {double.MaxValue,-double.MaxValue,double.MaxValue}.Concat(Enumerable.Repeat(0d,30)).ToArray()),2,2);
        var batch=Data(Bars(new[] {10d,10,10})).CalculateDEnvelope(1,2);Assert.Equal(new[] {20d,10,10},batch.OutputValues["MiddleBand"]);Assert.Equal(new[] {60d,10,10},batch.OutputValues["UpperBand"]);Assert.Equal(new[] {-20d,10,10},batch.OutputValues["LowerBand"]);
        var settled=Data(Bars(Enumerable.Repeat(10d,256).ToArray())).CalculateDEnvelope(3,2);Assert.Equal(10,settled.OutputValues["MiddleBand"][255],10);
    }
    private static void Check(Bar[] bars,int length,double factor)
    {
        var expected=BuiltInFormulaReferences.DEnvelopeOutputs(bars,length,factor);var batch=Data(bars).CalculateDEnvelope(length,factor);using var context=new ComputeContext();
        foreach(var key in expected.Keys) {Assert.Equal(expected[key],batch.OutputValues[key]);using var raw=IndicatorCompute.ComputeDEnvelopeFast(Data(bars),context,length,factor,key);Assert.Equal(expected[key],raw.Span.ToArray());}
        var state=new DEnvelopeState(length,factor);for(var replay=0;replay<2;replay++) {state.Reset();for(var i=0;i<bars.Length;i++) {state.Update(Native(new Bar(bars[i].Time,3,4,-2,-1,7)),false,true);foreach(var final in new[] {false,false,true}) {var actual=state.Update(Native(bars[i]),final,true);foreach(var key in expected.Keys)Assert.Equal(expected[key][i],actual.Outputs![key]);if(factor>=0)Assert.InRange(actual.Value,actual.Outputs!["LowerBand"],actual.Outputs["UpperBand"]);}}}
    }
    [Fact]
    public void SelectedClosesDriveTheCorrectionAndItsDeviation()
    {
        var prices=new[] {10d,10,0,-5};var data=Data(Bars(new[] {4d,4,4,4}));data.SetCustomValues(prices.ToList());var expected=BuiltInFormulaReferences.DEnvelopeOutputs(Bars(prices),3,2);using var context=new ComputeContext();
        using var raw=IndicatorCompute.ComputeDEnvelopeFast(data,context,3,2,"UpperBand");Assert.Equal(expected["UpperBand"],raw.Span.ToArray());Assert.Equal(expected["LowerBand"],data.CalculateDEnvelope(3,2).OutputValues["LowerBand"]);
    }
    [Fact]
    public void InvalidInputsNeverAdvanceNativeState()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            var state=new DEnvelopeState(2);var control=new DEnvelopeState(2);var first=Native(new Bar(DateTime.UnixEpoch,2,3,1,2,1));state.Update(first,true,true);control.Update(first,true,true);
            var values=new[] {2d,4,1,2,1};values[field]=invalid;var bad=new OhlcvBar("DE",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,values[0],values[1],values[2],values[3],values[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));var next=Native(new Bar(DateTime.UnixEpoch.AddMinutes(1),4,5,3,4,1));Assert.Equal(control.Update(next,true,true).Outputs!,state.Update(next,true,true).Outputs!);
        }
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(DEnvelope)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.DEnvelopeOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}

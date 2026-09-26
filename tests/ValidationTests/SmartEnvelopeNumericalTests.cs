using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class SmartEnvelopeNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static Bar[] Bars(double[] prices)=>prices.Select((v,i)=>new Bar(DateTime.UnixEpoch.AddMinutes(i),v,v,v,v,1)).ToArray();
    private static OhlcvBar Native(Bar b)=>new("DE",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    [Fact]
    public void CrossCoupledFeedbackAndFirstPriceSeedSurviveExtremeState()
    {
        foreach(var length in new[] {0,1,2,7,20,int.MaxValue})foreach(var factor in new[] {-2d,0,1,3,double.MaxValue})foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/8})
            Check(Bars(Enumerable.Range(0,20).Select(i=>(i%11-5)*scale).ToArray()),length,factor);
        Check(Array.Empty<Bar>(),14,1);Check(Bars(new[] {double.MaxValue,-double.MaxValue,double.MaxValue}.Concat(Enumerable.Repeat(0d,12)).ToArray()),2,3);
        Check(Bars(Enumerable.Repeat(double.MaxValue,8).ToArray()),2,1);
        var batch=Data(Bars(new[] {10d,12,8})).CalculateSmartEnvelope(2,1);Assert.Equal(new[] {10d,11,9.5},batch.OutputValues["UpperBand"]);Assert.Equal(new[] {10d,11,6.5},batch.OutputValues["LowerBand"]);Assert.Equal(new[] {10d,11,8},batch.OutputValues["MiddleBand"]);
        batch=Data(Bars(new[] {10d,12,8})).CalculateSmartEnvelope(2,0);Assert.Equal(new[] {10d,12,12},batch.OutputValues["UpperBand"]);Assert.Equal(new[] {10d,10,8},batch.OutputValues["LowerBand"]);
    }
    private static void Check(Bar[] bars,int length,double factor)
    {
        var expected=BuiltInFormulaReferences.SmartEnvelopeOutputs(bars,length,factor);var batch=Data(bars).CalculateSmartEnvelope(length,factor);using var context=new ComputeContext();
        foreach(var key in expected.Keys) {Assert.Equal(expected[key],batch.OutputValues[key]);using var raw=IndicatorCompute.ComputeSmartEnvelopeFast(Data(bars),context,length,factor,key);Assert.Equal(expected[key],raw.Span.ToArray());}
        var state=new SmartEnvelopeState(length,factor);for(var replay=0;replay<2;replay++) {state.Reset();for(var i=0;i<bars.Length;i++) {state.Update(Native(new Bar(bars[i].Time,3,4,-2,-1,7)),false,true);foreach(var final in new[] {false,false,true}) {var actual=state.Update(Native(bars[i]),final,true);foreach(var key in expected.Keys)Assert.Equal(expected[key][i],actual.Outputs![key]);}}}
    }
    [Fact]
    public void SelectedClosesDriveBothFeedbackBoundaries()
    {
        var prices=new[] {10d,10,0,-5};var data=Data(Bars(new[] {4d,4,4,4}));data.SetCustomValues(prices.ToList());var expected=BuiltInFormulaReferences.SmartEnvelopeOutputs(Bars(prices),3,2);using var context=new ComputeContext();
        using var raw=IndicatorCompute.ComputeSmartEnvelopeFast(data,context,3,2,"UpperBand");Assert.Equal(expected["UpperBand"],raw.Span.ToArray());Assert.Equal(expected["LowerBand"],data.CalculateSmartEnvelope(3,2).OutputValues["LowerBand"]);
    }
    [Fact]
    public void InvalidInputsNeverAdvanceNativeState()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            var state=new SmartEnvelopeState(2);var control=new SmartEnvelopeState(2);var first=Native(new Bar(DateTime.UnixEpoch,2,3,1,2,1));state.Update(first,true,true);control.Update(first,true,true);
            var values=new[] {2d,4,1,2,1};values[field]=invalid;var bad=new OhlcvBar("DE",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,values[0],values[1],values[2],values[3],values[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));var next=Native(new Bar(DateTime.UnixEpoch.AddMinutes(1),4,5,3,4,1));Assert.Equal(control.Update(next,true,true).Outputs!,state.Update(next,true,true).Outputs!);
        }
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(SmartEnvelope)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.SmartEnvelopeOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}

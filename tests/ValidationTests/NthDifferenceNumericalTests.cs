using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class NthDifferenceNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static OhlcvBar Native(Bar b)=>new("DIFF",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    private static Bar[] Bars(IEnumerable<double> values)=>values.Select((v,i)=>new Bar(DateTime.UnixEpoch.AddMinutes(i),0,v,v,v,1)).ToArray();
    [Fact]
    public void ExactBinomialFormMatchesSuccessiveDifferencesAndAnnihilatesPolynomials()
    {
        foreach(var lag in new[] {0,1,3,14})foreach(var order in new[] {0,1,2,3,10,32,64})foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/8}) Check(Bars(Enumerable.Range(0,80).Select(i=>(i%7-3)*scale)),lag,order);
        Check(Array.Empty<Bar>(),14,2);var constant=Bars(Enumerable.Repeat(double.MaxValue,70));Check(constant,1,64);Assert.Equal(0d,BuiltInFormulaReferences.NthDifferenceOutputs(constant,1,64)["Nodo"][69]);
        var cubic=Bars(Enumerable.Range(0,80).Select(i=>(double)i*i*i));Check(cubic,3,4);Assert.All(BuiltInFormulaReferences.NthDifferenceOutputs(cubic,3,4)["Nodo"].Skip(12),v=>Assert.Equal(0d,v));
        var quadratic=Bars(new[] {1d,4,9,16,25});Check(quadratic,1,2);Assert.Equal(new[] {1d,2,2,2,2},BuiltInFormulaReferences.NthDifferenceOutputs(quadratic,1,2)["Nodo"]);
    }
    private static void Check(Bar[] bars,int lag,int order)
    {
        var expected=BuiltInFormulaReferences.NthDifferenceOutputs(bars,lag,order)["Nodo"];Assert.Equal(expected,Data(bars).CalculateNthOrderDifferencingOscillator(lag,order).ChainedValues);var core=new double[bars.Length];OoplesFinance.StockIndicators.Core.OscillatorCore.NthOrderDifferencingOscillator(bars.Select(b=>b.Close).ToArray(),core,lag,order);Assert.Equal(expected,core);if(order==2) {using var context=new ComputeContext();using var raw=IndicatorCompute.ComputeNthOrderDifferencingOscillatorFast(Data(bars),context,lag);Assert.Equal(expected,raw.Span.ToArray());}using var state=new NthOrderDifferencingOscillatorState(lag,order);
        for(var replay=0;replay<2;replay++) {state.Reset();for(var i=0;i<bars.Length;i++) {state.Update(Native(new Bar(bars[i].Time,3,4,-2,-1,7)),false,true);foreach(var final in new[] {false,false,true})Assert.Equal(expected[i],state.Update(Native(bars[i]),final,true).Outputs!["Nodo"]);}}
    }
    [Fact]
    public void RawAndBatchUseTheSelectedSeries()
    {
        var original=Bars(Enumerable.Repeat(99d,5));var selected=new[] {1d,4,9,16,25};var expected=new[] {1d,2,2,2,2};var data=Data(original);data.SetCustomValues(selected.ToList());using var context=new ComputeContext();using var raw=IndicatorCompute.ComputeNthOrderDifferencingOscillatorFast(data,context,1);Assert.Equal(expected,raw.Span.ToArray());Assert.Equal(expected,data.CalculateNthOrderDifferencingOscillator(1,2).ChainedValues);
    }
    [Fact]
    public void InvalidFieldsCannotAdvanceLagHistory()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            using var state=new NthOrderDifferencingOscillatorState(1,2);using var control=new NthOrderDifferencingOscillatorState(1,2);var first=Native(Bars(new[] {3d})[0]);state.Update(first,true,true);control.Update(first,true,true);var v=new[] {0d,4,-2,1,1};v[field]=invalid;var bad=new OhlcvBar("DIFF",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,v[0],v[1],v[2],v[3],v[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));foreach(var b in Bars(new[] {4d,7,3,9,2,5}))Assert.Equal(control.Update(Native(b),true,true).Outputs!,state.Update(Native(b),true,true).Outputs!);
        }
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(NthOrderDifferencingOscillator)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.NthDifferenceOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}

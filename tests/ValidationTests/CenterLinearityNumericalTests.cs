using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class CenterLinearityNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static OhlcvBar Native(Bar b)=>new("GRAVITY",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    private static Bar[] Bars(IEnumerable<double> values)=>values.Select((v,i)=>new Bar(DateTime.UnixEpoch.AddMinutes(i),v,v,v,v,1)).ToArray();
    [Fact]
    public void BarIndexedDifferencesAndRollingCancellationMatchIndependentFractions()
    {
        foreach(var length in new[] {int.MinValue,0,1,2,3,14,int.MaxValue})foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/4})
            Check(Bars(Enumerable.Range(0,40).Select(i=>(i%7-3)*scale)),length);
        Check(Array.Empty<Bar>(),14);var hand=Bars(new[] {1d,2,1});Check(hand,2);Assert.Equal(new[] {0d,-2,-5},BuiltInFormulaReferences.CenterLinearityOutputs(hand,2)["Col"]);
        var cancellation=Bars(new[] {double.MaxValue*.75,0,0,0});var expected=BuiltInFormulaReferences.CenterLinearityOutputs(cancellation,2)["Col"];
        Assert.Equal(double.NegativeInfinity,expected[1]);Assert.True(double.IsFinite(expected[2]));Assert.True(expected[2]>0);Check(cancellation,2);
        Check(Bars(new[] {double.MaxValue,-double.MaxValue,double.Epsilon,0,1}),2);
        Assert.All(BuiltInFormulaReferences.CenterLinearityOutputs(hand,1)["Col"],v=>Assert.Equal(0d,v));
    }
    private static void Check(Bar[] bars,int length)
    {
        var expected=BuiltInFormulaReferences.CenterLinearityOutputs(bars,length)["Col"];
        Assert.Equal(expected,Data(bars).CalculateCenterOfLinearity(length).ChainedValues);
        using var context=new ComputeContext();using var raw=IndicatorCompute.ComputeCenterOfLinearityFast(Data(bars),context,length);Assert.Equal(expected,raw.Span.ToArray());
        var core=new double[bars.Length];OoplesFinance.StockIndicators.Core.OscillatorCore.CenterOfLinearity(bars.Select(b=>b.Close).ToArray(),core,length);Assert.Equal(expected,core);
        using var state=new CenterOfLinearityState(length);
        for(var replay=0;replay<2;replay++){state.Reset();for(var i=0;i<bars.Length;i++){state.Update(Native(new Bar(bars[i].Time,1,4,-3,2,1)),false,true);foreach(var final in new[] {false,false,true}){var result=state.Update(Native(bars[i]),final,true);Assert.Equal(expected[i],result.Value);Assert.Equal(expected[i],result.Outputs!["Col"]);}}}
    }
    [Fact]
    public void RawAndBatchKeepSelectedPrices()
    {
        var original=Bars(Enumerable.Repeat(9d,12));var selected=Enumerable.Range(0,12).Select(i=>(double)(i%7-2)).ToArray();var expected=BuiltInFormulaReferences.CenterLinearityOutputs(Bars(selected),3)["Col"];
        Assert.Contains(expected,v=>v!=0);var data=Data(original);data.SetCustomValues(selected.ToList());using var context=new ComputeContext();using var raw=IndicatorCompute.ComputeCenterOfLinearityFast(data,context,3);Assert.Equal(expected,raw.Span.ToArray());Assert.Equal(expected,data.CalculateCenterOfLinearity(3).ChainedValues);
    }
    [Fact]
    public void InvalidFieldsCannotAdvanceAnyHistoryOrIndex()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            using var state=new CenterOfLinearityState(3);using var control=new CenterOfLinearityState(3);var first=Native(Bars(new[] {1d})[0]);state.Update(first,true,true);control.Update(first,true,true);var v=new[] {0d,4,-2,1,1};v[field]=invalid;var bad=new OhlcvBar("COL",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,v[0],v[1],v[2],v[3],v[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));foreach(var bar in Bars(new[] {4d,7,3,9,2,5}))Assert.Equal(control.Update(Native(bar),true,true).Outputs!,state.Update(Native(bar),true,true).Outputs!);
        }
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(CenterOfLinearity)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.CenterLinearityOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}

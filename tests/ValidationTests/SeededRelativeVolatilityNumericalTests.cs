using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class SeededRelativeVolatilityNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static OhlcvBar Native(Bar b)=>new("RVI",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    private static IStreamingIndicatorState State(bool high,int length,int deviation)=>high?new RelativeVolatilityIndexHighState(length,deviation):new RelativeVolatilityIndexLowState(length,deviation);
    [Fact]
    public void DelayedSeedPopulationDeviationAndWilderRecurrenceMatchExactReference()
    {
        foreach(var high in new[] {false,true})foreach(var length in new[] {0,1,3,14})foreach(var deviation in new[] {0,1,2,10})foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/8})
            Check(Enumerable.Range(0,24).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),0,(i%5+2)*scale,-(i%3+2)*scale,0,1)).ToArray(),high,length,deviation);
        foreach(var high in new[] {false,true})
        {
            Check(Array.Empty<Bar>(),high,14,10);
            var flat=Enumerable.Range(0,8).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),double.MaxValue,double.MaxValue,double.MaxValue,double.MaxValue,1)).ToArray();Check(flat,high,3,2);Assert.Equal(new[] {0d,0,0,50,50,50,50,50},BuiltInFormulaReferences.SeededRelativeVolatilityOutputs(flat,high,3,2).Single().Value);
            var prices=new[] {1d,3,1,3,1};var bars=prices.Select((v,i)=>new Bar(DateTime.UnixEpoch.AddMinutes(i),0,v,v,0,1)).ToArray();Check(bars,high,2,2);Assert.Equal(new[] {0d,0,50,75,37.5},BuiltInFormulaReferences.SeededRelativeVolatilityOutputs(bars,high,2,2).Single().Value);
            var extreme=new[] {-double.MaxValue,double.MaxValue,-double.MaxValue,double.MaxValue,-double.MaxValue}.Select((v,i)=>new Bar(DateTime.UnixEpoch.AddMinutes(i),0,v,v,0,1)).ToArray();Check(extreme,high,3,2);
        }
    }
    private static void Check(Bar[] bars,bool high,int length,int deviation)
    {
        var expected=BuiltInFormulaReferences.SeededRelativeVolatilityOutputs(bars,high,length,deviation).Single().Value;Assert.All(expected,v=>Assert.InRange(v,0d,100d));var data=Data(bars);data.SetCustomValues(Enumerable.Repeat(99d,bars.Length).ToList());var batch=high?data.CalculateRelativeVolatilityIndexHigh(length,deviation):data.CalculateRelativeVolatilityIndexLow(length,deviation);Assert.Equal(expected,batch.ChainedValues);using var context=new ComputeContext();data=Data(bars);data.SetCustomValues(Enumerable.Repeat(99d,bars.Length).ToList());using var raw=high?IndicatorCompute.ComputeRelativeVolatilityIndexHighFast(data,context,length,deviation):IndicatorCompute.ComputeRelativeVolatilityIndexLowFast(data,context,length,deviation);Assert.Equal(expected,raw.Span.ToArray());var state=State(high,length,deviation);using var disposable=(IDisposable)state;
        for(var replay=0;replay<2;replay++) {state.Reset();for(var i=0;i<bars.Length;i++) {state.Update(Native(new Bar(bars[i].Time,3,4,-2,-1,7)),false,true);foreach(var final in new[] {false,false,true})Assert.Equal(expected[i],state.Update(Native(bars[i]),final,true).Outputs![high?"RviHigh":"RviLow"]);}}
    }
    [Fact]
    public void InvalidFieldsCannotContaminateSeedOrWilderState()
    {
        foreach(var high in new[] {false,true})foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            var state=State(high,2,2);var control=State(high,2,2);using var d1=(IDisposable)state;using var d2=(IDisposable)control;var first=Native(new Bar(DateTime.UnixEpoch,0,3,-2,1,1));state.Update(first,true,true);control.Update(first,true,true);var v=new[] {0d,4,-2,1,1};v[field]=invalid;var bad=new OhlcvBar("RVI",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,v[0],v[1],v[2],v[3],v[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));foreach(var close in new[] {4d,7,3,9,2,5}) {var next=Native(new Bar(DateTime.UnixEpoch.AddMinutes(1),close,close+2,close-1,close,1));Assert.Equal(control.Update(next,true,true).Outputs!,state.Update(next,true,true).Outputs!);}
        }
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(RelativeVolatilityIndexHigh)||c.IndicatorType==typeof(RelativeVolatilityIndexLow)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.SeededRelativeVolatilityOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}

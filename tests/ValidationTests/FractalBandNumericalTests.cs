using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class FractalBandNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static OhlcvBar Native(Bar b)=>new("FRACTAL",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    [Fact]
    public void FiveRealBarsAndFourStrictNeighborsConfirmRetainedExtrema()
    {
        var highs=new[] {2d,3,5,3,2,1,8,2,1,2,2};var lows=highs.Select(v=>-v).ToArray();
        var bars=Enumerable.Range(0,highs.Length).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),0,highs[i],lows[i],0,1)).ToArray();
        for(var count=0;count<=bars.Length;count++)Check(bars.Take(count).ToArray());
        Check(new[] {5d,3,2,1,0}.Select((v,i)=>new Bar(DateTime.UnixEpoch.AddMinutes(i),0,v,-v,0,1)).ToArray());
        var batch=Data(bars).CalculateFractalChaosBands();Assert.Equal(new[] {0d,0,0,0,5,5,5,5,8,8,8},batch.OutputValues["UpperBand"]);Assert.Equal(new[] {0d,0,0,0,-5,-5,-5,-5,-8,-8,-8},batch.OutputValues["LowerBand"]);
        foreach(var neighbor in new[] {0,1,3,4})foreach(var upper in new[] {false,true})
        {
            var tied=bars.Take(5).ToArray();var b=tied[neighbor];tied[neighbor]=new Bar(b.Time,0,upper?5:b.High,upper?b.Low:-5,0,1);Check(tied);
            var result=Data(tied).CalculateFractalChaosBands();Assert.Equal(0,result.OutputValues[upper?"UpperBand":"LowerBand"][4]);
        }
        foreach(var sign in new[] {-1d,1d})foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/16})
            Check(Enumerable.Range(0,24).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),sign*6*scale,(sign*6+i%5)*scale,(sign*6-i%7)*scale,sign*6*scale,1)).ToArray());
        var extreme=Enumerable.Range(0,8).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),double.MaxValue*.8,i==2?double.MaxValue:double.MaxValue*.9,i==2?double.MaxValue*.6:double.MaxValue*.7,double.MaxValue*.8,1)).ToArray();Check(extreme);
        Check(extreme.Select(b=>new Bar(b.Time,-b.Open,-b.Low,-b.High,-b.Close,b.Volume)).ToArray());
    }
    private static void Check(Bar[] bars)
    {
        var expected=BuiltInFormulaReferences.FractalBandOutputs(bars);var batch=Data(bars).CalculateFractalChaosBands();using var context=new ComputeContext();
        foreach(var key in expected.Keys) {Assert.Equal(expected[key],batch.OutputValues[key]);using var raw=IndicatorCompute.ComputeFractalChaosBandsFast(Data(bars),context,key);Assert.Equal(expected[key],raw.Span.ToArray());}
        using var state=new FractalChaosBandsState();for(var replay=0;replay<2;replay++) {state.Reset();for(var i=0;i<bars.Length;i++) {state.Update(Native(new Bar(bars[i].Time,0,double.MaxValue,-double.MaxValue,0,7)),false,true);foreach(var final in new[] {false,false,true}) {var actual=state.Update(Native(bars[i]),final,true);foreach(var key in expected.Keys)Assert.Equal(expected[key][i],actual.Outputs![key]);}}}
    }
    [Fact]
    public void InvalidInputsNeverAdvanceNativeHistory()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            using var state=new FractalChaosBandsState();using var control=new FractalChaosBandsState();
            foreach(var p in new[] {2d,3,5,3}) {var first=Native(new Bar(DateTime.UnixEpoch,0,p,-p,0,1));state.Update(first,true,true);control.Update(first,true,true);}
            var values=new[] {0d,10,-10,0,1};values[field]=invalid;var bad=new OhlcvBar("FRACTAL",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,values[0],values[1],values[2],values[3],values[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));var next=Native(new Bar(DateTime.UnixEpoch.AddMinutes(1),0,2,-2,0,1));Assert.Equal(control.Update(next,true,true).Outputs!,state.Update(next,true,true).Outputs!);
        }
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(FractalChaosBands)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.FractalBandOutputs(bars),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}

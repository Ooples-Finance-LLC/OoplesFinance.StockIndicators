using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class CycleAmplitudeNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static Bar[] Bars(double[] prices)=>prices.Select((v,i)=>new Bar(DateTime.UnixEpoch.AddMinutes(i),v,v,v,v,1)).ToArray();
    private static OhlcvBar Native(Bar b)=>new("ECA",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    [Fact]
    public void ExactEnergyKeepsLagExpirySubnormalAndExtendedPower()
    {
        foreach(var length in new[] {0,1,2,3,5,20,37})foreach(var delta in new[] {-1d,0,.1,.3,2,double.MaxValue})foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/8})
            Check(Bars(Enumerable.Range(0,48).Select(i=>(i%11-5)*scale).ToArray()),length,delta);
        Check(Array.Empty<Bar>(),20,.1);Check(Bars(new[] {-double.MaxValue,double.MaxValue,double.MaxValue,-double.MaxValue,double.MaxValue,-double.MaxValue,0d,0,0,0}),20,.1);
        var tiny=Data(Bars(new[] {0d,0,32*double.Epsilon,0,0,0,0,0})).CalculateEhlersCycleAmplitude(3);Assert.True(tiny.OutputValues["Eca"][2]>0);
        var large=Data(Bars(new[] {0d,0,double.MaxValue/8,0,0,0,0,0})).CalculateEhlersCycleAmplitude(20);Assert.True(large.OutputValues["Eca"][2]>0);Assert.All(large.OutputValues["Eca"],v=>Assert.True(double.IsFinite(v)));
        var flat=Data(Bars(Enumerable.Repeat(double.MaxValue,24).ToArray())).CalculateEhlersCycleAmplitude(3);Assert.All(flat.OutputValues["Eca"],v=>Assert.Equal(0d,v));
    }
    private static void Check(Bar[] bars,int length,double delta)
    {
        var expected=BuiltInFormulaReferences.CycleAmplitudeOutputs(bars,length,delta)["Eca"];var batch=Data(bars).CalculateEhlersCycleAmplitude(length,delta);Assert.Equal(expected,batch.OutputValues["Eca"]);using var context=new ComputeContext();using var raw=IndicatorCompute.ComputeEhlersCycleAmplitudeFast(Data(bars),context,length,delta);Assert.Equal(expected,raw.Span.ToArray());
        var core=new double[bars.Length];OscillatorCore.EhlersCycleAmplitude(bars.Select(b=>b.Close).ToArray(),core,length,delta);Assert.Equal(expected,core);
        using var state=new EhlersCycleAmplitudeState(length,delta);for(var replay=0;replay<2;replay++) {state.Reset();for(var i=0;i<bars.Length;i++) {state.Update(Native(new Bar(bars[i].Time,3,4,-2,-1,7)),false,true);foreach(var final in new[] {false,false,true})Assert.Equal(expected[i],state.Update(Native(bars[i]),final,true).Outputs!["Eca"]);}}
    }
    [Fact]
    public void SelectedPricesFeedBothLaggedEnergyWindows()
    {
        var prices=new[] {2d,7,3,8,4,9,1,0,5,2,9};var expected=BuiltInFormulaReferences.CycleAmplitudeOutputs(Bars(prices),3,.3)["Eca"];using var context=new ComputeContext();var data=Data(Bars(Enumerable.Repeat(4d,prices.Length).ToArray()));data.SetCustomValues(prices.ToList());Assert.Equal(expected,data.CalculateEhlersCycleAmplitude(3,.3).OutputValues["Eca"]);data=Data(Bars(Enumerable.Repeat(4d,prices.Length).ToArray()));data.SetCustomValues(prices.ToList());using var raw=IndicatorCompute.ComputeEhlersCycleAmplitudeFast(data,context,3,.3);Assert.Equal(expected,raw.Span.ToArray());
    }
    [Fact]
    public void InvalidFieldsAndDeltaNeverAdvanceStateOrEnterEmptyExecution()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            using var state=new EhlersCycleAmplitudeState(3);using var control=new EhlersCycleAmplitudeState(3);var first=Native(new Bar(DateTime.UnixEpoch,2,3,1,2,1));state.Update(first,true,true);control.Update(first,true,true);var values=new[] {2d,4,1,2,1};values[field]=invalid;var bad=new OhlcvBar("ECA",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,values[0],values[1],values[2],values[3],values[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));foreach(var b in Bars(new[] {4d,7,3,9,2,5}))Assert.Equal(control.Update(Native(b),true,true).Outputs!,state.Update(Native(b),true,true).Outputs!);
        }
        foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity}) {Assert.Throws<ArgumentOutOfRangeException>(()=>new EhlersCycleAmplitudeState(delta:invalid));Assert.Throws<ArgumentOutOfRangeException>(()=>Data(Array.Empty<Bar>()).CalculateEhlersCycleAmplitude(delta:invalid));Assert.Throws<ArgumentOutOfRangeException>(()=>OscillatorCore.EhlersCycleAmplitude(Array.Empty<double>(),Array.Empty<double>(),delta:invalid));using var context=new ComputeContext();Assert.Throws<ArgumentOutOfRangeException>(()=>IndicatorCompute.ComputeEhlersCycleAmplitudeFast(Data(Array.Empty<Bar>()),context,delta:invalid));}
        Assert.Throws<ArgumentException>(()=>OscillatorCore.EhlersCycleAmplitude(new[] {1d},Array.Empty<double>()));
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(EhlersCycleAmplitude)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.CycleAmplitudeOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}

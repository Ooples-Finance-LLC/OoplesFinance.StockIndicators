using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class EfficientTrendStepNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static Bar[] Bars(double[] prices)=>prices.Select((v,i)=>new Bar(DateTime.UnixEpoch.AddMinutes(i),v,v,v,v,1)).ToArray();
    private static OhlcvBar Native(Bar b)=>new("ETS",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    [Fact]
    public void EfficiencyBlendsDoubledPopulationDeviationsAndHoldsBoundaryTies()
    {
        foreach(var length in new[] {0,1,2,3,14})foreach(var fast in new[] {0,1,2,3,7})foreach(var slow in new[] {1,2,5,14})foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/8})
            Check(Bars(Enumerable.Range(0,16).Select(i=>(i%11-5)*scale).ToArray()),length,fast,slow);
        Check(Array.Empty<Bar>(),100,50,200);
        var ties=Bars(new[] {0d,2,4,2});Check(ties,1,2,3);var hand=Data(ties).CalculateEfficientTrendStepChannel(1,2,3);Assert.Equal(new[] {0d,0,4,4},hand.OutputValues["MiddleBand"]);Assert.Equal(new[] {0d,2,6,6},hand.OutputValues["UpperBand"]);Assert.Equal(new[] {0d,-2,2,2},hand.OutputValues["LowerBand"]);
        var tiny=Bars(new[] {0d,double.Epsilon});Check(tiny,1,2,3);var small=Data(tiny).CalculateEfficientTrendStepChannel(1,2,3);Assert.Equal(double.Epsilon,small.OutputValues["UpperBand"][1]);Assert.Equal(-double.Epsilon,small.OutputValues["LowerBand"][1]);
        var extreme=Bars(new[] {0d,double.MaxValue,0,-double.MaxValue});Check(extreme,1,2,3);var big=Data(extreme).CalculateEfficientTrendStepChannel(1,2,3);Assert.Equal(new[] {0d,0,0,0},big.OutputValues["MiddleBand"]);Assert.All(big.OutputValues["UpperBand"],v=>Assert.True(double.IsFinite(v)));Assert.All(big.OutputValues["LowerBand"],v=>Assert.True(double.IsFinite(v)));
        Check(Bars(new[] {double.MaxValue,double.MaxValue,double.MaxValue}),2,2,3);
    }
    private static void Check(Bar[] bars,int length,int fast,int slow)
    {
        var expected=BuiltInFormulaReferences.EfficientTrendStepOutputs(bars,length,fast,slow);var batch=Data(bars).CalculateEfficientTrendStepChannel(length,fast,slow);using var context=new ComputeContext();
        foreach(var key in new[] {"UpperBand","MiddleBand","LowerBand"}) {Assert.Equal(expected[key],batch.OutputValues[key]);using var raw=IndicatorCompute.ComputeEfficientTrendStepFast(Data(bars),context,length,fast,slow,key);Assert.Equal(expected[key],raw.Span.ToArray());}
        using var state=new EfficientTrendStepChannelState(length,fast,slow);for(var replay=0;replay<2;replay++) {state.Reset();for(var i=0;i<bars.Length;i++) {state.Update(Native(new Bar(bars[i].Time,3,4,-2,-1,7)),false,true);foreach(var final in new[] {false,false,true}) {var actual=state.Update(Native(bars[i]),final,true);foreach(var key in new[] {"UpperBand","MiddleBand","LowerBand"})Assert.Equal(expected[key][i],actual.Outputs![key]);}}}
    }
    [Fact]
    public void SelectedPricesDriveEfficiencyAndBothDeviations()
    {
        var prices=new[] {2d,4,6,3,8};var expected=BuiltInFormulaReferences.EfficientTrendStepOutputs(Bars(prices),2,3,4);using var context=new ComputeContext();foreach(var batch in new[] {false,true})
        {
            var data=Data(Bars(new[] {4d,4,4,4,4}));data.SetCustomValues(prices.ToList());if(batch) {var output=data.CalculateEfficientTrendStepChannel(2,3,4);foreach(var key in new[] {"UpperBand","MiddleBand","LowerBand"})Assert.Equal(expected[key],output.OutputValues[key]);}else {using var raw=IndicatorCompute.ComputeEfficientTrendStepFast(data,context,2,3,4,"UpperBand");Assert.Equal(expected["UpperBand"],raw.Span.ToArray());}
        }
    }
    [Fact]
    public void InvalidFieldsNeverAdvanceNativeState()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            using var state=new EfficientTrendStepChannelState(2,3,4);using var control=new EfficientTrendStepChannelState(2,3,4);var first=Native(new Bar(DateTime.UnixEpoch,2,3,1,2,1));state.Update(first,true,true);control.Update(first,true,true);
            var values=new[] {2d,4,1,2,1};values[field]=invalid;var bad=new OhlcvBar("ETS",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,values[0],values[1],values[2],values[3],values[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));var next=Native(new Bar(DateTime.UnixEpoch.AddMinutes(1),4,5,3,4,1));Assert.Equal(control.Update(next,true,true).Outputs!,state.Update(next,true,true).Outputs!);
        }
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(EfficientTrendStepChannel)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.EfficientTrendStepOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}

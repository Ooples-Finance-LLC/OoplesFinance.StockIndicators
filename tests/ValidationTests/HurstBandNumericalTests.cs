using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class HurstBandNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static Bar[] Bars(double[] prices)=>prices.Select((v,i)=>new Bar(DateTime.UnixEpoch.AddMinutes(i),v,v,v,v,1)).ToArray();
    private static OhlcvBar Native(Bar b)=>new("HURST",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    [Fact]
    public void DelayedZerosAndPartialMeanPreserveAllSixPercentageEnvelopes()
    {
        foreach(var length in new[] {0,1,2,3,10})foreach(var factors in new[] {(1.6,2.6,4.2),(-200d,0d,100d),(double.MaxValue,-double.MaxValue,.5)})foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/8})
            Check(Bars(Enumerable.Range(0,16).Select(i=>(i%11-5)*scale).ToArray()),length,factors.Item1,factors.Item2,factors.Item3);
        Check(Array.Empty<Bar>(),10,1.6,2.6,4.2);Check(Bars(Enumerable.Repeat(double.MaxValue,12).ToArray()),2,-200,200,100);
        foreach(var length in new[] {1058,1059})Check(Bars(Enumerable.Range(0,533).Select(i=>(double)(i+1)).ToArray()),length,1.6,2.6,4.2);
        var batch=Data(Bars(new[] {2d,4,6,8,10})).CalculateHurstBands(2,100,200,300);
        Assert.Equal(new[] {0d,0,1,3,5},batch.OutputValues["MiddleBand"]);Assert.Equal(new[] {0d,0,2,6,10},batch.OutputValues["UpperInnerBand"]);Assert.Equal(new[] {0d,0,0,0,0},batch.OutputValues["LowerInnerBand"]);
        Assert.Equal(new[] {0d,0,3,9,15},batch.OutputValues["UpperOuterBand"]);Assert.Equal(new[] {0d,0,-1,-3,-5},batch.OutputValues["LowerOuterBand"]);Assert.Equal(new[] {0d,0,4,12,20},batch.OutputValues["UpperExtremeBand"]);Assert.Equal(new[] {0d,0,-2,-6,-10},batch.OutputValues["LowerExtremeBand"]);
    }
    private static void Check(Bar[] bars,int length,double inner,double outer,double extreme)
    {
        var expected=BuiltInFormulaReferences.HurstBandOutputs(bars,length,inner,outer,extreme);var batch=Data(bars).CalculateHurstBands(length,inner,outer,extreme);using var context=new ComputeContext();
        foreach(var key in expected.Keys) {Assert.Equal(expected[key],batch.OutputValues[key]);using var raw=IndicatorCompute.ComputeHurstBandsFast(Data(bars),context,length,inner,outer,extreme,key);Assert.Equal(expected[key],raw.Span.ToArray());}
        using var state=new HurstBandsState(length,inner,outer,extreme);for(var replay=0;replay<2;replay++) {state.Reset();for(var i=0;i<bars.Length;i++) {state.Update(Native(new Bar(bars[i].Time,3,4,-2,-1,7)),false,true);foreach(var final in new[] {false,false,true}) {var actual=state.Update(Native(bars[i]),final,true);foreach(var key in expected.Keys)Assert.Equal(expected[key][i],actual.Outputs![key]);}}}
    }
    [Fact]
    public void SelectedPricesFeedDelayedHistory()
    {
        var prices=new[] {2d,4,6,8,10};var data=Data(Bars(Enumerable.Repeat(4d,5).ToArray()));data.SetCustomValues(prices.ToList());var expected=BuiltInFormulaReferences.HurstBandOutputs(Bars(prices),2,100,200,300);using var context=new ComputeContext();
        foreach(var key in expected.Keys) {using var raw=IndicatorCompute.ComputeHurstBandsFast(data,context,2,100,200,300,key);Assert.Equal(expected[key],raw.Span.ToArray());}var batch=data.CalculateHurstBands(2,100,200,300);foreach(var key in expected.Keys)Assert.Equal(expected[key],batch.OutputValues[key]);
    }
    [Fact]
    public void InvalidFieldsAndMultipliersNeverAdvanceNativeHistory()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            using var state=new HurstBandsState(2);using var control=new HurstBandsState(2);foreach(var p in new[] {2d,3,4,5}) {var first=Native(new Bar(DateTime.UnixEpoch,p,p+1,p-1,p,1));state.Update(first,true,true);control.Update(first,true,true);}
            var values=new[] {2d,4,1,2,1};values[field]=invalid;var bad=new OhlcvBar("HURST",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,values[0],values[1],values[2],values[3],values[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));var next=Native(new Bar(DateTime.UnixEpoch.AddMinutes(1),4,5,3,4,1));Assert.Equal(control.Update(next,true,true).Outputs!,state.Update(next,true,true).Outputs!);
        }
        foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity}) {Assert.Throws<ArgumentOutOfRangeException>(()=>new HurstBandsState(innerMult:invalid));Assert.Throws<ArgumentOutOfRangeException>(()=>new HurstBandsState(outerMult:invalid));Assert.Throws<ArgumentOutOfRangeException>(()=>new HurstBandsState(extremeMult:invalid));Assert.Throws<ArgumentOutOfRangeException>(()=>Data(Array.Empty<Bar>()).CalculateHurstBands(innerMult:invalid));using var context=new ComputeContext();Assert.Throws<ArgumentOutOfRangeException>(()=>IndicatorCompute.ComputeHurstBandsFast(Data(Array.Empty<Bar>()),context,outerMult:invalid));}
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(HurstBands)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.HurstBandOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}

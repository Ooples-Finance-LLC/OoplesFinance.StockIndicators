using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class CenterGravityNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static OhlcvBar Native(Bar b)=>new("GRAVITY",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    private static Bar[] Bars(IEnumerable<double> values)=>values.Select((v,i)=>new Bar(DateTime.UnixEpoch.AddMinutes(i),v,v,v,v,1)).ToArray();
    [Fact]
    public void ExactRollingMomentsMatchDirectWeightedFractionsWithStagedRounding()
    {
        foreach(var length in new[] {0,1,2,3,8,30,int.MaxValue})foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/8})Check(Bars(Enumerable.Range(0,48).Select(i=>(i%7-3)*scale)),length);
        Check(Array.Empty<Bar>(),10);var hand=Bars(new[] {1d,2,4});Check(hand,3);Assert.Equal(new[] {1d,2d-4d/3,2d-11d/7},BuiltInFormulaReferences.CenterGravityOutputs(hand,3)["Ecog"]);
        var constant=Bars(Enumerable.Repeat(double.MaxValue,12));Check(constant,3);Assert.All(BuiltInFormulaReferences.CenterGravityOutputs(constant,3)["Ecog"].Skip(2),v=>Assert.Equal(0d,v));
        var tinySum=Bars(new[] {double.MaxValue,-double.MaxValue,double.Epsilon,0,0,1,2,4});Check(tinySum,3);Assert.True(double.IsInfinity(BuiltInFormulaReferences.CenterGravityOutputs(tinySum,3)["Ecog"][2]));
    }
    private static void Check(Bar[] bars,int length)
    {
        var expected=BuiltInFormulaReferences.CenterGravityOutputs(bars,length)["Ecog"];Assert.Equal(expected,Data(bars).CalculateEhlersCenterofGravityOscillator(length).ChainedValues);
        using var context=new ComputeContext();using var raw=IndicatorCompute.ComputeEhlersCenterofGravityOscillatorFast(Data(bars),context,length);using var alias=IndicatorCompute.ComputeEhlersCenterOfGravityOscillatorFast(Data(bars),context,length);Assert.Equal(expected,raw.Span.ToArray());Assert.Equal(expected,alias.Span.ToArray());
        var prices=bars.Select(b=>b.Close).ToArray();var core=new double[bars.Length];OoplesFinance.StockIndicators.Core.OscillatorCore.EhlersCenterofGravityOscillator(prices,core,length);Assert.Equal(expected,core);OoplesFinance.StockIndicators.Core.OscillatorCore.EhlersCenterOfGravityOscillator(prices,core,length);Assert.Equal(expected,core);
        using var state=new EhlersCenterofGravityOscillatorState(length);for(var replay=0;replay<2;replay++){state.Reset();for(var i=0;i<bars.Length;i++){state.Update(Native(new Bar(bars[i].Time,1,4,-3,2,1)),false,true);foreach(var final in new[] {false,false,true}){var result=state.Update(Native(bars[i]),final,true);Assert.Equal(expected[i],result.Value);Assert.Equal(expected[i],result.Outputs!["Ecog"]);}}}
    }
    [Fact]
    public void BothRawAliasesAndBatchUseSelectedPrices()
    {
        var original=Bars(new[] {9d,9,9});var selected=new[] {1d,2,4};var expected=new[] {1d,2d-4d/3,2d-11d/7};var data=Data(original);data.SetCustomValues(selected.ToList());using var context=new ComputeContext();using var raw=IndicatorCompute.ComputeEhlersCenterofGravityOscillatorFast(data,context,3);using var alias=IndicatorCompute.ComputeEhlersCenterOfGravityOscillatorFast(data,context,3);Assert.Equal(expected,raw.Span.ToArray());Assert.Equal(expected,alias.Span.ToArray());Assert.Equal(expected,data.CalculateEhlersCenterofGravityOscillator(3).ChainedValues);
    }
    [Fact]
    public void InvalidFieldsCannotAdvanceEitherMoment()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            using var state=new EhlersCenterofGravityOscillatorState(3);using var control=new EhlersCenterofGravityOscillatorState(3);var first=Native(Bars(new[] {1d})[0]);state.Update(first,true,true);control.Update(first,true,true);var v=new[] {0d,4,-2,1,1};v[field]=invalid;var bad=new OhlcvBar("GRAVITY",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,v[0],v[1],v[2],v[3],v[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));foreach(var bar in Bars(new[] {4d,7,3,9,2,5}))Assert.Equal(control.Update(Native(bar),true,true).Outputs!,state.Update(Native(bar),true,true).Outputs!);
        }
    }
    [Fact]
    public void DirectStochasticConsumerRetainsBatchRawAndNativeAgreement()
    {
        foreach(var length in new[] {1,3,8,14})foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/8})
        {
            var bars=Bars(Enumerable.Range(0,48).Select(i=>(i%7-3)*scale));var expected=Data(bars).CalculateEhlersStochasticCenterOfGravityOscillator(length).ChainedValues;using var context=new ComputeContext();using var raw=IndicatorCompute.ComputeEhlersStochasticCenterOfGravityOscillatorFast(Data(bars),context,length);Assert.Equal(expected,raw.Span.ToArray());using var state=new EhlersStochasticCenterOfGravityOscillatorState(length);for(var i=0;i<bars.Length;i++)Assert.Equal(expected[i],state.Update(Native(bars[i]),true,true).Value);
        }
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(EhlersCenterofGravityOscillator)||c.IndicatorType==typeof(EhlersCenterOfGravityOscillator)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.CenterGravityOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}
